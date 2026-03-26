---
description: "Full end-to-end bug analysis and fix pipeline. Use when: you want to analyze an exception and automatically get a code fix, investigate a crash end-to-end, analyze a stacktrace and fix the root cause, diagnose and repair an application error"
name: "Analyze and Fix Bug"
agent: "agent"
tools: [read, search, edit, view_image, web, todo, agent]
argument-hint: "Attach a screenshot of the exception OR paste the stacktrace/log fragment. Optionally include the path to a log file."
---

You are running the full bug analysis and fix pipeline. Follow these steps in order:

## Step 1: Collect Input

The user has provided: `{{input}}`

If no log file path was provided, search the workspace for log files:
- `appsettings.json` → check for a configured log file path
- Common locations: `logs/`, `App_Data/logs/`, root `*.log`, `*.json` log files

## Step 2: Run the Bug Analyzer

Invoke the **Bug Analyzer** agent with:
- The exception / stacktrace / screenshot provided by the user
- The log file path (if found or provided)

Wait for the Bug Analyzer to complete and return its structured `bugReport` JSON.

## Step 3: Present Findings and Confirm

Show the user:
1. The **root cause hypothesis** from `codeAnalysis.rootCause`
2. The **throw site**: file, line, method
3. A summary of **what the logs revealed** (key errors and context entries)
4. Any **missing information** that could affect fix quality
5. The **confidence level**

Then ask:
> "The analysis is complete. Shall I proceed with the fix? (yes / no / I need to provide more information)"

If the user says **no** or wants to provide more context, stop and wait.

## Step 4: Run the Bug Fixer

If the user confirms, invoke the **Bug Fixer** agent with the full `bugReport` JSON from Step 2.

## Step 5: Report

Present the fix summary from the Bug Fixer to the user, including:
- Files changed
- What was changed and why
- Any caveats or recommended follow-up actions (e.g. test cases to add, NewRelic query to run once connected)
