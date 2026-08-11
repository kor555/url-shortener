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

## API

- `GET /api/health` — health check endpoint used by the frontend
- `POST /api/urls` — create a short link (`{ "originalUrl": "https://..." }`)
- `GET /api/urls` — list all short links
- `GET /api/urls/{code}` — get one short link
- `PUT /api/urls/{code}` — edit `originalUrl` and/or `isActive`
- `DELETE /api/urls/{code}` — permanently delete a short link
- `GET /{code}` — redirects to the original URL (302), or 410 if inactive, or 404 if unknown

Short codes are the Base62 encoding of the link's database id. Short links are displayed using the `ShortUrl:BaseUrl` setting in `appsettings.json` (`https://gul.fy`).

During development, the Vite dev server proxies `/api/*` requests to the .NET API on port 5104.

## Testing the gul.fy domain locally

To make `gul.fy` resolve to your machine so real short links (e.g. `https://gul.fy/3`) can be clicked during development, add a hosts entry yourself:

```bash
echo "127.0.0.1 gul.fy" | sudo tee -a /etc/hosts
```

The API listens on `http://localhost:5104` (no TLS in Development), so once the entry is added, visit `http://gul.fy:5104/{code}` to exercise the redirect. `UseHttpsRedirection` is skipped in Development for this reason.
