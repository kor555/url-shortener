import { useEffect, useState } from 'react';
import './App.css';

interface HealthResponse {
  status: string;
  service: string;
}

function App() {
  const [health, setHealth] = useState<HealthResponse | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetch('/api/health')
      .then((res) => {
        if (!res.ok) throw new Error(`API returned ${res.status}`);
        return res.json() as Promise<HealthResponse>;
      })
      .then(setHealth)
      .catch((err: Error) => setError(err.message));
  }, []);

  return (
    <main className="app">
      <h1>URL Shortener</h1>
      <p className="subtitle">React + TypeScript frontend · .NET backend</p>

      <section className="status-card">
        <h2>API Status</h2>
        {error && <p className="error">Could not reach backend: {error}</p>}
        {!error && !health && <p>Checking backend...</p>}
        {health && (
          <dl>
            <div>
              <dt>Status</dt>
              <dd>{health.status}</dd>
            </div>
            <div>
              <dt>Service</dt>
              <dd>{health.service}</dd>
            </div>
          </dl>
        )}
      </section>
    </main>
  );
}

export default App;
