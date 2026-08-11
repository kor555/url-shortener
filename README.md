# URL Shortener Monorepo

A monorepo with a React + TypeScript frontend and an ASP.NET Core backend.

## Structure

```
url-shortener/
├── frontend/          # React + TypeScript (Vite)
├── backend/
│   └── UrlShortener.Api/   # ASP.NET Core Web API
├── package.json       # Root npm scripts
└── UrlShortener.slnx  # .NET solution
```

## Prerequisites

- Node.js 18+
- .NET 10 SDK

## Getting Started

Install dependencies:

```bash
npm install
```

Run both frontend and backend:

```bash
npm run dev
```

Or run them separately:

```bash
npm run dev:frontend   # http://localhost:5173
npm run dev:backend    # http://localhost:5104
```

## Build

```bash
npm run build
```

## Tests

Run the backend unit test suite:

```bash
npm run test
```

Or invoke `dotnet test` directly:

```bash
dotnet test backend/UrlShortener.Api.Tests
```

## API

- `GET /api/health` — health check endpoint used by the frontend
- `POST /api/urls` — create a short link
  - body: `{ "originalUrl": "https://...", "platformTargets": [...], "customCode": "..." }`
  - `platformTargets` (optional): array of `{ "platform": "android" | "ios" | ..., "url": "..." }` overrides
  - `customCode` (optional): a custom name for the link (e.g. `"Coffee"`) instead of the auto-generated code
- `GET /api/urls` — list all short links
- `GET /api/urls/{code}` — get one short link
- `PUT /api/urls/{code}` — edit a short link
  - body: `{ "originalUrl": "...", "isActive": true, "platformTargets": [...], "customCode": "..." }`, all fields optional
  - `platformTargets`: omit/`null` to leave untouched, or send a full (possibly empty) array to replace it
  - `customCode`: omit/`null` to leave untouched, `""` to clear it back to the auto-generated code, or any other value to set/rename it
- `DELETE /api/urls/{code}` — permanently delete a short link
- `GET /{code}` — redirects to the resolved destination (302), or 410 if inactive, or 404 if unknown
  - `{code}` matches either a link's custom name or its auto-generated code
  - the destination is picked from `platformTargets` based on the request's User-Agent (`android`/`ios`, more platforms may be added later), falling back to `originalUrl`
  - each visit increments the link's `viewCount` (and the matched platform's `clickCount`), but only while the link is active

A link's response includes `id`, `code`, `shortUrl`, `originalUrl`, `isActive`, `createdAt`, `updatedAt`, `viewCount`, `customCode`, and `platformTargets` (each with `platform`, `url`, `clickCount`).

Auto-generated codes are the Base62 encoding of the link's database id; a link keeps resolving by that code even after a custom name is set. Short links are displayed using the `ShortUrl:BaseUrl` setting in `appsettings.json` (`https://gul.fy`).

During development, the Vite dev server proxies `/api/*` requests to the .NET API on port 5104.

## Testing the gul.fy domain locally

To make `gul.fy` resolve to your machine so real short links (e.g. `https://gul.fy/3`) can be clicked during development, add a hosts entry yourself:

```bash
echo "127.0.0.1 gul.fy" | sudo tee -a /etc/hosts
```

The API listens on `http://localhost:5104` (no TLS in Development), so once the entry is added, visit `http://gul.fy:5104/{code}` to exercise the redirect. `UseHttpsRedirection` is skipped in Development for this reason.

## TODO

- Search/filter the link list by custom name or by the original link's domain
- Support more platforms in Platform-specific mode, with the ability to select 2 or more platforms at once
