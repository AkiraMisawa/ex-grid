# Pictures for pull requests

This branch holds the screenshots and GIFs that pull request descriptions show (AGENTS.md, "A pull request
shows what it changes"). It is never merged, and no workflow runs on it.

- One folder per pull request, named after its branch.
- A description links a picture by the commit that added it,
  `https://github.com/<owner>/<repo>/blob/<sha>/<folder>/<file>?raw=true`, so a later push cannot change what a
  merged pull request shows.
- Nothing here is rewritten or removed once a description shows it. A push refused because another pull
  request's pictures arrived first is rebased onto them and pushed again, never forced.
- Keep folder and file names short: an image link over 150 characters was seen to arrive as a plain link.
