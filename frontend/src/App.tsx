import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import './App.css';

interface PlatformTarget {
  platform: string;
  url: string;
}

interface PlatformTargetView extends PlatformTarget {
  clickCount: number;
}

interface UrlItem {
  id: number;
  code: string;
  shortUrl: string;
  originalUrl: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  viewCount: number;
  platformTargets: PlatformTargetView[];
}

type LinkMode = 'single' | 'platform';

const findTarget = (targets: PlatformTarget[], platform: string) =>
  targets.find((t) => t.platform === platform)?.url ?? '';

const buildPlatformTargets = (androidUrl: string, iosUrl: string): PlatformTarget[] => {
  const targets: PlatformTarget[] = [];
  if (androidUrl.trim()) targets.push({ platform: 'android', url: androidUrl.trim() });
  if (iosUrl.trim()) targets.push({ platform: 'ios', url: iosUrl.trim() });
  return targets;
};

function ModeTabs({ mode, onChange }: { mode: LinkMode; onChange: (mode: LinkMode) => void }) {
  return (
    <div className="mode-tabs" role="tablist">
      <button
        type="button"
        role="tab"
        className={mode === 'single' ? 'active' : ''}
        aria-selected={mode === 'single'}
        onClick={() => onChange('single')}
      >
        Single destination
      </button>
      <button
        type="button"
        role="tab"
        className={mode === 'platform' ? 'active' : ''}
        aria-selected={mode === 'platform'}
        onClick={() => onChange('platform')}
      >
        Platform-specific
      </button>
    </div>
  );
}

function PlatformFields({
  androidUrl,
  iosUrl,
  onAndroidChange,
  onIosChange,
}: {
  androidUrl: string;
  iosUrl: string;
  onAndroidChange: (value: string) => void;
  onIosChange: (value: string) => void;
}) {
  return (
    <div className="platform-fields">
      <label>
        Android
        <input
          type="text"
          placeholder="play.google.com/store/apps/..."
          value={androidUrl}
          onChange={(e) => onAndroidChange(e.target.value)}
        />
      </label>
      <label>
        iOS
        <input
          type="text"
          placeholder="apps.apple.com/app/..."
          value={iosUrl}
          onChange={(e) => onIosChange(e.target.value)}
        />
      </label>
    </div>
  );
}

function App() {
  const [items, setItems] = useState<UrlItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const [createMode, setCreateMode] = useState<LinkMode>('single');
  const [originalUrl, setOriginalUrl] = useState('');
  const [androidUrl, setAndroidUrl] = useState('');
  const [iosUrl, setIosUrl] = useState('');

  const [editingCode, setEditingCode] = useState<string | null>(null);
  const [editMode, setEditMode] = useState<LinkMode>('single');
  const [editValue, setEditValue] = useState('');
  const [editAndroidUrl, setEditAndroidUrl] = useState('');
  const [editIosUrl, setEditIosUrl] = useState('');

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
      const platformTargets = createMode === 'platform' ? buildPlatformTargets(androidUrl, iosUrl) : [];
      const res = await fetch('/api/urls', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ originalUrl, platformTargets }),
      });
      if (!res.ok) {
        const body = await res.json().catch(() => null);
        throw new Error(body?.error ?? `API returned ${res.status}`);
      }
      const created = (await res.json()) as UrlItem;
      setItems((prev) => [created, ...prev]);
      setOriginalUrl('');
      setAndroidUrl('');
      setIosUrl('');
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
    setEditMode(item.platformTargets.length > 0 ? 'platform' : 'single');
    setEditValue(item.originalUrl);
    setEditAndroidUrl(findTarget(item.platformTargets, 'android'));
    setEditIosUrl(findTarget(item.platformTargets, 'ios'));
  };

  const cancelEdit = () => {
    setEditingCode(null);
    setEditValue('');
    setEditAndroidUrl('');
    setEditIosUrl('');
  };

  const saveEdit = async (item: UrlItem) => {
    setError(null);
    try {
      const platformTargets = editMode === 'platform' ? buildPlatformTargets(editAndroidUrl, editIosUrl) : [];
      const res = await fetch(`/api/urls/${item.code}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ originalUrl: editValue, platformTargets }),
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

      <ModeTabs mode={createMode} onChange={setCreateMode} />

      <form className="shorten-form" onSubmit={handleSubmit}>
        <div className="url-field">
          {createMode === 'platform' && <label>Default destination (fallback)</label>}
          <input
            type="text"
            placeholder="example.com/very/long/path"
            value={originalUrl}
            onChange={(e) => setOriginalUrl(e.target.value)}
            required
          />
        </div>

        {createMode === 'platform' && (
          <PlatformFields
            androidUrl={androidUrl}
            iosUrl={iosUrl}
            onAndroidChange={setAndroidUrl}
            onIosChange={setIosUrl}
          />
        )}

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
              <span className="total-clicks">{item.viewCount} clicks</span>
            </div>

            {editingCode === item.code ? (
              <div className="edit-section">
                <ModeTabs mode={editMode} onChange={setEditMode} />

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

                {editMode === 'platform' && (
                  <PlatformFields
                    androidUrl={editAndroidUrl}
                    iosUrl={editIosUrl}
                    onAndroidChange={setEditAndroidUrl}
                    onIosChange={setEditIosUrl}
                  />
                )}
              </div>
            ) : (
              <div className="original-row">
                <span className="original-url">{item.originalUrl}</span>
                <button type="button" onClick={() => startEdit(item)}>
                  Edit
                </button>
              </div>
            )}

            {editingCode !== item.code && item.platformTargets.length > 0 && (
              <ul className="platform-list">
                {item.platformTargets.map((target) => (
                  <li key={target.platform}>
                    <div className="platform-list-header">
                      <strong>{target.platform}</strong>
                      <span className="click-count">{target.clickCount} clicks</span>
                    </div>
                    <div className="platform-list-url">{target.url}</div>
                  </li>
                ))}
              </ul>
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
