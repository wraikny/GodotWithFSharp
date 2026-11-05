# Agent Instructions

When committing changes created by Codex, include the following trailer in the commit message:
`Co-authored-by: Codex {Codex Model Name} <codex@openai.com>`

Codex may execute `git commit` only after the user explicitly approves both:

1. The exact proposed commit message, including the required co-author trailer.
2. The complete staged changes to be included in that commit.

Before requesting approval, stage the intended changes and present the complete staged diff and the exact proposed commit message for review.
Approval applies only to the reviewed staged changes and commit message. If either changes after approval, obtain approval again before committing.
Do not execute `git commit` without this prior approval.
