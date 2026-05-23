open System
open System.Collections.Concurrent
open System.IO
open System.Net
open System.Text.Json
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection

type WordEntry =
    { Category: string
      Word: string }

type GuessOutcome =
    | Valid
    | Invalid of string

type GameState =
    { Category: string
      Word: string
      GuessedLetters: char list
      WrongGuesses: int
      Message: string
      GameOver: bool
      Won: bool }

let maxWrongGuesses = 6
let sessionCookieName = "hangman-session-id"

type LibraryCategory =
    { Name: string
      Words: string array }

let randomLock = obj ()
let random = Random()
let games = ConcurrentDictionary<string, GameState>()
let mutable shuffledDeck : WordEntry list = []

let loadWordBank () =
    let basePath = AppContext.BaseDirectory
    let libraryPath = Path.Combine(basePath, "word-library.json")
    let json = File.ReadAllText libraryPath
    let categories = JsonSerializer.Deserialize<LibraryCategory array>(json)

    match categories with
    | null
    | [||] -> failwith "Word library is empty."
    | categoryEntries ->
        categoryEntries
        |> Array.collect (fun category ->
            category.Words
            |> Array.map (fun word ->
                { Category = category.Name
                  Word = word.Trim().ToLowerInvariant() }))
        |> Array.filter (fun entry -> not (String.IsNullOrWhiteSpace entry.Word))
        |> Array.toList

let wordBank = loadWordBank ()

let shuffleWords (words: WordEntry list) =
    words
    |> List.toArray
    |> fun items ->
        for index = items.Length - 1 downto 1 do
            let swapIndex = random.Next(index + 1)
            let current = items[index]
            items[index] <- items[swapIndex]
            items[swapIndex] <- current

        items |> Array.toList

let pickNextWord () =
    lock randomLock (fun () ->
        if List.isEmpty shuffledDeck then
            shuffledDeck <- shuffleWords wordBank

        match shuffledDeck with
        | next :: remaining ->
            shuffledDeck <- remaining
            next
        | [] -> failwith "Shuffled word deck unexpectedly empty.")

let createNewGame message =
    let entry = pickNextWord ()

    { Category = entry.Category
      Word = entry.Word.ToLowerInvariant()
      GuessedLetters = []
      WrongGuesses = 0
      Message = message
      GameOver = false
      Won = false }

let getMaskedWord (game: GameState) =
    game.Word
    |> Seq.map (fun c ->
        if List.contains c game.GuessedLetters then
            string c
        else
            "_")
    |> String.concat " "

let getGuessedLettersDisplay (game: GameState) =
    match game.GuessedLetters with
    | [] -> "(none)"
    | letters ->
        letters
        |> List.map (fun c -> string (Char.ToUpperInvariant c))
        |> String.concat ", "

let hasWon (game: GameState) =
    game.Word
    |> Seq.distinct
    |> Seq.forall (fun c -> List.contains c game.GuessedLetters)

let hangmanStages =
    [|
        [ " +---+"
          " |   |"
          "     |"
          "     |"
          "     |"
          "     |"
          "=========" ]
        [ " +---+"
          " |   |"
          " O   |"
          "     |"
          "     |"
          "     |"
          "=========" ]
        [ " +---+"
          " |   |"
          " O   |"
          " |   |"
          "     |"
          "     |"
          "=========" ]
        [ " +---+"
          " |   |"
          " O   |"
          "/|   |"
          "     |"
          "     |"
          "=========" ]
        [ " +---+"
          " |   |"
          " O   |"
          "/|\\  |"
          "     |"
          "     |"
          "=========" ]
        [ " +---+"
          " |   |"
          " O   |"
          "/|\\  |"
          "/    |"
          "     |"
          "=========" ]
        [ " +---+"
          " |   |"
          " O   |"
          "/|\\  |"
          "/ \\  |"
          "     |"
          "=========" ]
    |]

let getHangmanDrawing (game: GameState) =
    hangmanStages[game.WrongGuesses] |> String.concat "\n"

let validateGuess (input: string) =
    if String.IsNullOrWhiteSpace input then
        Invalid "Enter a single letter."
    else
        let trimmed = input.Trim()

        if trimmed.Length <> 1 then
            Invalid "Enter exactly one letter."
        else
            let letter = Char.ToLowerInvariant trimmed[0]

            if not (Char.IsLetter letter) then
                Invalid "Only alphabetical characters are allowed."
            else
                Valid

let applyGuess input (game: GameState) =
    match validateGuess input with
    | Invalid message ->
        { game with Message = message }
    | Valid ->
        let letter = Char.ToLowerInvariant(input.Trim()[0])

        if List.contains letter game.GuessedLetters then
            { game with Message = $"You already guessed '{Char.ToUpperInvariant letter}'." }
        else
            let updatedGuesses = game.GuessedLetters @ [ letter ]

            if game.Word.Contains(letter) then
                let candidate =
                    { game with
                        GuessedLetters = updatedGuesses
                        Message = $"Good guess: '{Char.ToUpperInvariant letter}' is in the word." }

                if hasWon candidate then
                    { candidate with
                        Message = $"You won. The word was \"{candidate.Word}\". Wrong guesses used: {candidate.WrongGuesses}."
                        GameOver = true
                        Won = true }
                else
                    candidate
            else
                let wrongGuesses = game.WrongGuesses + 1
                let candidate =
                    { game with
                        GuessedLetters = updatedGuesses
                        WrongGuesses = wrongGuesses
                        Message = $"Sorry, '{Char.ToUpperInvariant letter}' is not in the word." }

                if wrongGuesses >= maxWrongGuesses then
                    { candidate with
                        Message = $"You lost. The word was \"{candidate.Word}\"."
                        GameOver = true
                        Won = false }
                else
                    candidate

let encode (text: string) = WebUtility.HtmlEncode text

let renderPage sessionId (game: GameState) =
    let maskedWord = getMaskedWord game
    let guessedLetters = getGuessedLettersDisplay game
    let drawing = getHangmanDrawing game
    let statusClass =
        if game.GameOver then
            if game.Won then "status win" else "status lose"
        else
            "status"

    let formSection =
        if game.GameOver then
            """
            <form method="post" action="/play-again" class="play-again">
              <button type="submit">Play Again</button>
            </form>
            """
        else
            """
            <form method="post" action="/guess" class="guess-form">
              <label for="guess">Enter a letter</label>
              <div class="guess-row">
                <input id="guess" name="guess" type="text" maxlength="1" autocomplete="off" autofocus />
                <button type="submit">Guess</button>
              </div>
            </form>
            """

    $"""<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>F# Hangman</title>
  <style>
    :root {{
      --bg-top: #f3efe4;
      --bg-bottom: #d9e6f2;
      --panel: rgba(255, 252, 247, 0.92);
      --ink: #1f2a36;
      --muted: #5b6875;
      --accent: #b85c38;
      --accent-dark: #8f4528;
      --good: #1f7a4d;
      --bad: #9f2d2d;
      --border: rgba(31, 42, 54, 0.12);
      --shadow: 0 18px 40px rgba(31, 42, 54, 0.14);
    }}

    * {{ box-sizing: border-box; }}

    body {{
      margin: 0;
      min-height: 100vh;
      font-family: Georgia, "Times New Roman", serif;
      color: var(--ink);
      background:
        radial-gradient(circle at top left, rgba(255,255,255,0.7), transparent 30%%),
        linear-gradient(160deg, var(--bg-top), var(--bg-bottom));
      display: grid;
      place-items: center;
      padding: 24px;
    }}

    .card {{
      width: min(920px, 100%%);
      background: var(--panel);
      border: 1px solid var(--border);
      border-radius: 24px;
      box-shadow: var(--shadow);
      overflow: hidden;
      backdrop-filter: blur(10px);
    }}

    .hero {{
      padding: 28px 28px 18px;
      background: linear-gradient(135deg, rgba(184, 92, 56, 0.14), rgba(255,255,255,0));
      border-bottom: 1px solid var(--border);
    }}

    .eyebrow {{
      margin: 0 0 8px;
      text-transform: uppercase;
      letter-spacing: 0.14em;
      font-size: 0.8rem;
      color: var(--muted);
    }}

    h1 {{
      margin: 0;
      font-size: clamp(2rem, 4vw, 3.4rem);
      line-height: 1;
    }}

    .subtitle {{
      margin: 12px 0 0;
      color: var(--muted);
      font-size: 1rem;
      max-width: 60ch;
    }}

    .layout {{
      display: grid;
      grid-template-columns: 320px 1fr;
      gap: 0;
    }}

    .drawing-panel {{
      padding: 28px;
      background: rgba(31, 42, 54, 0.04);
      border-right: 1px solid var(--border);
    }}

    .drawing-panel pre {{
      margin: 0;
      padding: 20px;
      border-radius: 16px;
      background: #1f2a36;
      color: #f7f2ea;
      font-size: 1rem;
      line-height: 1.15;
      overflow-x: auto;
    }}

    .content {{
      padding: 28px;
    }}

    .meta {{
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: 14px;
      margin-bottom: 22px;
    }}

    .meta-card {{
      padding: 16px;
      border-radius: 16px;
      background: rgba(255, 255, 255, 0.72);
      border: 1px solid var(--border);
    }}

    .meta-card strong {{
      display: block;
      margin-bottom: 6px;
      font-size: 0.86rem;
      text-transform: uppercase;
      letter-spacing: 0.08em;
      color: var(--muted);
    }}

    .word {{
      margin: 0 0 18px;
      font-size: clamp(1.8rem, 4vw, 2.6rem);
      letter-spacing: 0.18em;
      font-weight: 700;
    }}

    .status {{
      margin: 0 0 20px;
      padding: 14px 16px;
      border-radius: 14px;
      background: rgba(184, 92, 56, 0.1);
      border: 1px solid rgba(184, 92, 56, 0.16);
    }}

    .status.win {{
      background: rgba(31, 122, 77, 0.1);
      border-color: rgba(31, 122, 77, 0.16);
    }}

    .status.lose {{
      background: rgba(159, 45, 45, 0.1);
      border-color: rgba(159, 45, 45, 0.18);
    }}

    .guess-form,
    .play-again {{
      display: grid;
      gap: 12px;
    }}

    label {{
      font-weight: 700;
    }}

    .guess-row {{
      display: flex;
      gap: 12px;
    }}

    input[type="text"] {{
      width: 96px;
      border: 1px solid var(--border);
      border-radius: 14px;
      padding: 14px 16px;
      font-size: 1.2rem;
      text-align: center;
      text-transform: uppercase;
      background: white;
    }}

    button {{
      border: 0;
      border-radius: 14px;
      padding: 14px 18px;
      background: var(--accent);
      color: white;
      font: inherit;
      font-weight: 700;
      cursor: pointer;
    }}

    button:hover {{
      background: var(--accent-dark);
    }}

    .footer-note {{
      margin-top: 18px;
      color: var(--muted);
      font-size: 0.95rem;
    }}

    @media (max-width: 760px) {{
      .layout {{
        grid-template-columns: 1fr;
      }}

      .drawing-panel {{
        border-right: 0;
        border-bottom: 1px solid var(--border);
      }}

      .meta {{
        grid-template-columns: 1fr;
      }}

      .guess-row {{
        flex-direction: column;
      }}

      input[type="text"],
      button {{
        width: 100%%;
      }}
    }}
  </style>
</head>
<body>
  <main class="card">
    <section class="hero">
      <p class="eyebrow">F# Web Conversion</p>
      <h1>Hangman</h1>
      <p class="subtitle">Browser-based Hangman built from the CLI requirements. Guess one letter at a time before the full 6-part drawing is complete.</p>
    </section>

    <section class="layout">
      <aside class="drawing-panel">
        <pre>{encode drawing}</pre>
      </aside>

      <section class="content">
        <div class="meta">
          <div class="meta-card">
            <strong>Category</strong>
            <span>{encode game.Category}</span>
          </div>
          <div class="meta-card">
            <strong>Wrong Guesses</strong>
            <span>{game.WrongGuesses} / {maxWrongGuesses}</span>
          </div>
          <div class="meta-card">
            <strong>Guessed Letters</strong>
            <span>{encode guessedLetters}</span>
          </div>
          <div class="meta-card">
            <strong>Session</strong>
            <span>{encode sessionId}</span>
          </div>
        </div>

        <p class="word">{encode maskedWord}</p>
        <p class="{statusClass}">{encode game.Message}</p>

        {formSection}

        <p class="footer-note">Rules: invalid or duplicate guesses do not consume turns. Correct guesses reveal every matching letter.</p>
        <p class="footer-note">Words come from a categorized library and are shuffled without reuse until the full deck is exhausted.</p>
      </section>
    </section>
  </main>
</body>
</html>"""

let createSessionId (context: HttpContext) =
    let sessionId = Guid.NewGuid().ToString("N")

    context.Response.Cookies.Append(
        sessionCookieName,
        sessionId,
        CookieOptions(
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax
        )
    )

    sessionId

let ensureSessionId (context: HttpContext) : string =
    match context.Request.Cookies.TryGetValue sessionCookieName with
    | true, existing ->
        let existingId = existing |> Option.ofObj |> Option.defaultValue String.Empty

        if String.IsNullOrWhiteSpace existingId then
            createSessionId context
        else
            existingId
    | false, _ ->
        createSessionId context

let getOrCreateGame sessionId =
    games.GetOrAdd(sessionId, fun _ -> createNewGame "New game started. Enter your first guess.")

let saveGame sessionId game =
    games[sessionId] <- game

let builder = WebApplication.CreateBuilder()
builder.Services.AddRouting() |> ignore

let app = builder.Build()

app.MapGet("/", Func<HttpContext, IResult>(fun context ->
    let sessionId = ensureSessionId context
    let game = getOrCreateGame sessionId
    Results.Content(renderPage sessionId game, "text/html; charset=utf-8")
))
|> ignore

app.MapPost("/guess", Func<HttpContext, Threading.Tasks.Task<IResult>>(fun context ->
    task {
        let sessionId = ensureSessionId context
        let game = getOrCreateGame sessionId
        let! form = context.Request.ReadFormAsync()
        let guess = form["guess"].ToString()

        let updatedGame =
            if game.GameOver then
                { game with Message = "The game is over. Start a new game to continue." }
            else
                applyGuess guess game

        saveGame sessionId updatedGame
        return Results.Content(renderPage sessionId updatedGame, "text/html; charset=utf-8")
    }
))
|> ignore

app.MapPost("/play-again", Func<HttpContext, IResult>(fun context ->
    let sessionId = ensureSessionId context
    let game = createNewGame "New game started. A fresh word has been selected."
    saveGame sessionId game
    Results.Content(renderPage sessionId game, "text/html; charset=utf-8")
))
|> ignore

app.Run()
