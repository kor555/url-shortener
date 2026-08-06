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

During development, the Vite dev server proxies `/api/*` requests to the .NET API on port 5104.
