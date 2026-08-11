import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import './App.css';

interface UrlItem {
  id: number;
  code: string;
  shortUrl: string;
  originalUrl: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

function App() {
  const [items, setItems] = useState<UrlItem[]>([]);
  const [originalUrl, setOriginalUrl] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [editingCode, setEditingCode] = useState<string | null>(null);
  const [editValue, setEditValue] = useState('');

  const loadItems = () => {
    fetch('/api/urls')
      .then((res) => {
        if (!res.ok) throw new Error(`API returned ${res.status}`);
        return res.json() as Promise<UrlItem[]>;
      })
      .then(setItems)
      .catch((err: Error) => setError(err.message));
  };

  useEffect(() => {
    loadItems();
  }, []);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      const res = await fetch('/api/urls', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ originalUrl }),
      });
      if (!res.ok) {
        const body = await res.json().catch(() => null);
        throw new Error(body?.error ?? `API returned ${res.status}`);
      }
      const created = (await res.json()) as UrlItem;
      setItems((prev) => [created, ...prev]);
      setOriginalUrl('');
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setSubmitting(false);
    }
  };

  const handleToggleActive = async (item: UrlItem) => {
    setError(null);
    try {
      const res = await fetch(`/api/urls/${item.code}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ isActive: !item.isActive }),
      });
      if (!res.ok) throw new Error(`API returned ${res.status}`);
      const updated = (await res.json()) as UrlItem;
      setItems((prev) => prev.map((i) => (i.code === updated.code ? updated : i)));
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const startEdit = (item: UrlItem) => {
    setEditingCode(item.code);
    setEditValue(item.originalUrl);
  };

  const cancelEdit = () => {
    setEditingCode(null);
    setEditValue('');
  };

  const saveEdit = async (item: UrlItem) => {
    setError(null);
    try {
      const res = await fetch(`/api/urls/${item.code}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ originalUrl: editValue }),
      });
      if (!res.ok) {
        const body = await res.json().catch(() => null);
        throw new Error(body?.error ?? `API returned ${res.status}`);
      }
      const updated = (await res.json()) as UrlItem;
      setItems((prev) => prev.map((i) => (i.code === updated.code ? updated : i)));
      cancelEdit();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const handleDelete = async (item: UrlItem) => {
    if (!window.confirm(`Permanently delete ${item.shortUrl}? This cannot be undone.`)) return;
    setError(null);
    try {
      const res = await fetch(`/api/urls/${item.code}`, { method: 'DELETE' });
      if (!res.ok) throw new Error(`API returned ${res.status}`);
      setItems((prev) => prev.filter((i) => i.code !== item.code));
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text).catch(() => {});
  };

  return (
    <main className="app">
      <h1>URL Shortener</h1>
      <p className="subtitle">Shorten a link, then edit, deactivate, or delete it anytime.</p>

      <form className="shorten-form" onSubmit={handleSubmit}>
        <input
          type="text"
          placeholder="example.com/very/long/path"
          value={originalUrl}
          onChange={(e) => setOriginalUrl(e.target.value)}
          required
        />
        <button type="submit" disabled={submitting}>
          {submitting ? 'Shortening…' : 'Shorten'}
        </button>
      </form>

      {error && <p className="error">{error}</p>}

      <ul className="url-list">
        {items.map((item) => (
          <li key={item.code} className={item.isActive ? '' : 'inactive'}>
            <div className="url-row">
              <a href={item.shortUrl} target="_blank" rel="noreferrer" className="short-link">
                {item.shortUrl}
              </a>
              <button type="button" onClick={() => copyToClipboard(item.shortUrl)}>
                Copy
              </button>
            </div>

            {editingCode === item.code ? (
              <div className="edit-row">
                <input
                  type="text"
                  value={editValue}
                  onChange={(e) => setEditValue(e.target.value)}
                />
                <button type="button" onClick={() => saveEdit(item)}>
                  Save
                </button>
                <button type="button" onClick={cancelEdit}>
                  Cancel
                </button>
              </div>
            ) : (
              <div className="original-row">
                <span className="original-url">{item.originalUrl}</span>
                <button type="button" onClick={() => startEdit(item)}>
                  Edit
                </button>
              </div>
            )}

            <div className="actions-row">
              <label>
                <input
                  type="checkbox"
                  checked={item.isActive}
                  onChange={() => handleToggleActive(item)}
                />
                Active
              </label>
              <button type="button" className="danger" onClick={() => handleDelete(item)}>
                Delete
              </button>
            </div>
          </li>
        ))}
        {items.length === 0 && <p className="empty">No shortened links yet.</p>}
      </ul>
    </main>
  );
}

export default App;
