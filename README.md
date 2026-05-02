# CS20200-Hangman-Project
Term project for CS-20200 at KAIST. This is a CLI-based Hangman game where players guess hidden words across multiple categories while a 6-stage ASCII hangman tracks incorrect guesses. Developed using F# and .NET 10.

# CLI Hangman Game - CS-20200 Term Project

## Overview
This is a command-line Hangman game developed for the CS-20200: Programming Principles course. The project is implemented in **F#** using **.NET 10**[cite: 1, 2]. The game selects a random word from a category, and the player must guess the word before the 6-stage ASCII hangman is completed.

## Requirements Reference
The detailed functional requirements for this project can be found in the submitted `hangman_requirements.pdf`. This project was designed to satisfy the project specifications outlined in `spec (2).pdf`.

## Prerequisites
* **.NET 10 SDK** (Required for building and running the F# code).

## How to Run
1. Clone this repository to your local machine.
2. Open a terminal or command prompt in the project root directory.
3. Run the following command to start the game:
   ```bash
   dotnet run



Gameplay Instructions
At the start, the system selects a word and displays its category and blanks.  

Enter a single alphabetical letter to make a guess.  

You are allowed a maximum of 6 wrong guesses

After each game, you will be prompted to play again or exit.

Use of Large Language Models (LLM)
This section is required by the project specification.

What I used the LLM for: I used an LLM to help structure the initial LaTeX requirements document.

Manual changes/reprompting: I had to manually adjust the ASCII drawing stages in the LaTeX code because the LLM initially struggled with escaping backslashes (\) correctly for the hangman drawing.

Main point the LLM could not do: The LLM was not able to perfectly align the specific 6-stage drawing logic with the cumulative ASCII requirements without multiple corrections to the visual formatting.

Author
Name: Biniam Mena

Student ID: --------

Course: CS-20200: Programming Principles (Spring 2026)
