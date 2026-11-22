# Use wallhaven as the only wallpaper source

- Status: Accepted
- Date: 2026-11-21

## Context and Problem Statement

Salt.er needs a large, high-quality wallpaper catalog that it can search and
download from programmatically. Which source should it use, and should it
support several?

## Decision Drivers

- A documented, stable public API with search, sorting and filtering.
- Full-resolution images with resolution, ratio and color metadata.
- Content suitable by default, with explicit opt-in for anything else.
- A small app with few moving parts.

## Considered Options

- wallhaven (<https://wallhaven.cc>) only
- Several sources behind an abstraction (for example Unsplash, Bing)
- Local folders only

## Decision Outcome

Chosen option: wallhaven only. Its API offers the sorting modes (toplist,
latest, hot, random), category and purity filters, ratio, minimum resolution
and color search that the feed needs, and it works without an account.

Without an API key wallhaven serves safe-for-work content only. The key is
optional in Salt.er: with it, the user can also include Sketchy and NSFW
content. wallhaven allows 45 API requests per minute per user, and the client
enforces that limit (see [architecture](../architecture.md#network-behavior)).

### Consequences

- Good: one client, one query model, no source abstraction to maintain.
- Good: the app works immediately, with no sign-up.
- Bad: Salt.er depends on a third-party service; if wallhaven changes its API
  or becomes unavailable, the feed stops until the client is updated.
- Bad: wallpaper rights belong to their authors; Salt.er links back to each
  wallpaper's page and does not redistribute images.
- Neutral: other sources are a non-goal. Revisiting that decision needs a new
  ADR.
