# Design guide (source of the GitHub Pages site)

`docs/index.html` is a static, interactive reader. It fetches the Markdown chapters in this folder at runtime, renders them with marked.js and draws diagrams with Mermaid. The Markdown is the source of truth.

## Layout

```text
docs/index.html        the reader (nav, search, dark mode, EN/FA switch, widgets)
docs/guide/en/*.md     English chapters (done)
docs/guide/fa/*.md     Persian chapters (same file names; next step)
docs/.nojekyll         serve files as-is on GitHub Pages
```

## Chapters

| Group | Chapter |
| --- | --- |
| Start | 00 Start here |
| Why | 01 Problem · 02 Assumptions and constraints |
| What | 03 Solution overview · 04 Decisions (ADRs) |
| How it is built | 05 Data model and ERD · 06 State machines · 07 Core logic · 08 Idempotency · 09 Rate limits and fairness · 10 Code structure |
| Proof | 11 Edge cases · 12 Testing and coverage · 13 Load test |
| Honesty | 14 Trade-offs and growth |

## Preview locally

The reader uses `fetch`, so it needs HTTP (not `file://`):

```bash
cd docs && python3 -m http.server 8000   # then open http://localhost:8000
```

## Publish

`.github/workflows/pages.yml` deploys `docs/` on every push to `main` that touches `docs/`, and can be run by hand from the Actions tab. It first fails the build if a page listed in the menu has no Markdown file.

One-time setup: repository Settings → Pages → Build and deployment → Source: **GitHub Actions**.

## Add a chapter

Add `NN-name.md` under `guide/en` (and `guide/fa`), then add one row to `CHAPTERS` in `index.html`.
