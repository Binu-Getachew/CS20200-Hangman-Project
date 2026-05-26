# CS20200-Hangman-Project

Term project for CS-20200 at KAIST. This version implements the Hangman requirements as a browser-based F# web app using ASP.NET Core and .NET 10.

## Overview

The original requirements describe a command-line Hangman game where players guess hidden words across multiple categories while a 6-stage ASCII hangman tracks incorrect guesses. This implementation keeps the same gameplay rules and converts the interface to the web.

## Requirements Reference

The detailed functional requirements for this project can be found in the submitted `hangman_requirements.pdf`. This project was designed to satisfy the project specifications outlined in `spec (2).pdf`.

## What it implements

- Categorized word library loaded from `word-library.csv`
- Shuffle-based word selection without reuse until the deck is exhausted
- Category and blank word display
- Case-insensitive single-letter guesses
- Invalid input handling without consuming a turn
- Duplicate guess handling without consuming a turn
- Correct guess reveal across all positions
- 6-stage hangman drawing for incorrect guesses
- Wrong guess counter
- Display of guessed letters in entry order
- Win and lose conditions
- Play-again flow

## Prerequisites

- .NET 10 SDK

## Files

- `HangmanWeb.fsproj`
- `Program.fs`
- `word-library.csv`

## Word library behavior

The app does not pick from a tiny hardcoded list with replacement.

- Words are loaded from a categorized CSV file
- `word-library.csv` was imported from `hangman_words_by_category.csv` and augmented with the previously generated words in matching categories
- The full library is shuffled into a deck
- Each new game uses the next word in the deck
- Once the deck is empty, the app reshuffles the full library and starts again

To expand the game, add more words or categories to `word-library.csv`.

## Run locally

Install the .NET 10 SDK, then run:

```bash
dotnet run --project HangmanWeb.fsproj
```

Open the local URL printed by ASP.NET Core in your browser.

## Use of Large Language Models (LLM)

An LLM was used to help convert the provided Hangman requirements into a working F# web implementation, including the ASP.NET Core routing, server-rendered HTML, and the categorized word-library approach.

Manual changes and review were still needed for project structure, requirements alignment, word reuse behavior, and preserving the assignment-specific gameplay rules.

## Author

Name: Biniam Mena

Student ID: --------

Course: CS-20200: Programming Principles (Spring 2026)
