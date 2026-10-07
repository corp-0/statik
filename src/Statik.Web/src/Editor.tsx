import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { getMyPage, saveMyPage, deleteMyPage, ApiError, errorMessage } from './api';
import type { Page, User } from './api';
import CodeEditor from './CodeEditor';

const emptyPage: Page = { bodyTemplate: '<main></main>', css: '' };

export default function Editor({ user, onDirtyChange, publicOrigin }: { user: User; onDirtyChange: (dirty: boolean) => void; publicOrigin: string }) {
  const [page, setPage] = useState<Page>(emptyPage);
  const [saved, setSaved] = useState<Page | null>(null);
  const [loaded, setLoaded] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const dirty = loaded && (page.bodyTemplate !== (saved ?? emptyPage).bodyTemplate || page.css !== (saved ?? emptyPage).css);
  const publicUrl = `${publicOrigin}/${encodeURIComponent(user.username)}`;

  async function load() {
    setError('');
    try {
      const result = await getMyPage().catch(error => {
        if (error instanceof ApiError && error.status === 404) return null;
        throw error;
      });
      setSaved(result);
      setPage(result ?? emptyPage);
      setLoaded(true);
    } catch (error) {
      setError(errorMessage(error));
    }
  }

  useEffect(() => { void load(); }, []);

  useEffect(() => {
    onDirtyChange(dirty);
    return () => onDirtyChange(false);
  }, [dirty, onDirtyChange]);

  useEffect(() => {
    if (!dirty) return;
    function beforeUnload(event: BeforeUnloadEvent) { event.preventDefault(); }
    window.addEventListener('beforeunload', beforeUnload);
    return () => window.removeEventListener('beforeunload', beforeUnload);
  }, [dirty]);

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError('');
    setMessage('');
    try {
      const result = await saveMyPage(page);
      setSaved(result);
      setMessage('Page saved. Your changes are public.');
    } catch (error) {
      setError(errorMessage(error));
    } finally {
      setBusy(false);
    }
  }

  async function deletePage() {
    if (!window.confirm('Delete your public page? Its saved HTML and CSS will be removed.')) return;
    setBusy(true);
    setError('');
    setMessage('');
    try {
      await deleteMyPage();
      setSaved(null);
      setPage(emptyPage);
      setMessage('Page deleted. You can create a new one below.');
    } catch (error) {
      setError(errorMessage(error));
    } finally {
      setBusy(false);
    }
  }

  const saveLabel = saved ? 'Save page' : 'Create page';
  const loadingState = error ? <button onClick={() => void load()}>Retry loading page</button> : <p>Loading page...</p>;

  return (
    <section>
      <h2>Page editor</h2>
      <p>Signed in as {user.displayName} (@{user.username}).</p>
      {error && <p role="alert">{error}</p>}
      <p role="status">{message}</p>
      {!loaded ? loadingState : <>
        {saved ? <p><a href={publicUrl} target="_blank" rel="noopener noreferrer">View saved public page (new tab)</a></p>
          : <p>You do not have a page yet. Create one below.</p>}
        <p>Save publishes your HTML/Scriban and CSS immediately. JavaScript is not supported.</p>
        <p>Available template fields: <code>{'{{ profile.username }}'}</code>, <code>{'{{ profile.display_name }}'}</code>, <code>{'{{ profile.about }}'}</code>.</p>
        <p id="editor-keyboard-help">Tab indents, Shift+Tab unindents. Press Escape, then Tab to move out of an editor.</p>
        <form onSubmit={save}>
          <fieldset disabled={busy}>
            <legend>Page source</legend>
            <CodeEditor id="bodyTemplate" label="HTML / Scriban body" language="html" maxLength={1_000_000} disabled={busy}
              value={page.bodyTemplate} onChange={value => { setPage({ ...page, bodyTemplate: value }); setMessage(''); }} />
            <CodeEditor id="css" label="CSS" language="css" maxLength={1_000_000} disabled={busy}
              value={page.css} onChange={value => { setPage({ ...page, css: value }); setMessage(''); }} />
            <button type="submit">{busy ? 'Please wait...' : saveLabel}</button>{' '}
            {saved && <button type="button" onClick={() => void deletePage()}>Delete page</button>}
          </fieldset>
        </form>
        {dirty && <p>Unsaved changes.</p>}
      </>}
    </section>
  );
}
