import { useEffect, useState } from 'react';
import { getCurrentUser, logout as logoutAccount, ApiError, errorMessage } from './api';
import type { User } from './api';
import AuthForm from './AuthForm';
import Editor from './Editor';
import AccountRecovery from './AccountRecovery';
import AdminUsers from './AdminUsers';
import { sites } from './sites';
import type { Sites } from './sites';

export default function App() {
  if (window.location.origin === sites.publicOrigin) {
    return <main>
      <h1>STATIK</h1>
      <h2>BZZZZZZZZZZZZZZZZZZZZZZZZ</h2>
      <p>bzzzzzzzzzzzzzz.</p>
      <nav aria-label="Account">
        <a href={`${sites.appOrigin}/#/login`}>Log in</a>{' '}
        <a href={`${sites.appOrigin}/#/signup`}>Sign up</a>{' '}
        <a href={`${sites.appOrigin}/#/editor`}>Editor</a>
      </nav>
    </main>;
  }
  if (window.location.origin !== sites.appOrigin) return <p>Open Statik at <a href={sites.publicOrigin}>{sites.publicOrigin}</a>.</p>;
  return <ManagementApp sites={sites} />;
}

function ManagementApp({ sites }: { sites: Sites }) {
  const [route, setRoute] = useState(window.location.hash || '#/');
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [loggingOut, setLoggingOut] = useState(false);
  const [dirty, setDirty] = useState(false);

  async function loadSession() {
    setLoading(true);
    setError('');
    try {
      setUser(await getCurrentUser());
    } catch (error) {
      if (!(error instanceof ApiError && error.status === 401)) setError(errorMessage(error));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => { void loadSession(); }, []);

  useEffect(() => {
    function navigate() {
      if (dirty && !window.confirm('Leave the editor and discard your unsaved changes?')) {
        window.history.replaceState(null, '', route);
        return;
      }
      setRoute(window.location.hash || '#/');
    }
    window.addEventListener('hashchange', navigate);
    return () => window.removeEventListener('hashchange', navigate);
  }, [dirty, route]);

  async function logout() {
    if (dirty && !window.confirm('Log out and discard your unsaved changes?')) return;
    setLoggingOut(true);
    setError('');
    try {
      await logoutAccount();
      setDirty(false);
      setUser(null);
      window.location.hash = '/';
    } catch (error) {
      setError(errorMessage(error));
    } finally {
      setLoggingOut(false);
    }
  }

  let content;
  if (loading) {
    content = <p role="status">Loading session...</p>;
  } else if (route === '#/') {
    content = <>
      <h2>Your personal webpage</h2>
      <p>Create and edit your page on Statik.</p>
      <p>{user ? <a href="#/editor">Open editor</a> : <><a href="#/signup">Sign up</a> or <a href="#/login">log in</a> to get started.</>}</p>
    </>;
  } else if (route === '#/editor') {
    content = user ? <Editor key={user.username} user={user} onDirtyChange={setDirty} publicOrigin={sites.publicOrigin} />
      : <><p>Log in to edit your page.</p><AuthForm signup={false} onLogin={setUser} relaxedPasswords={sites.relaxedPasswords} /></>;
  } else if (route === '#/login' || route === '#/signup') {
    content = user ? <p>You are logged in. <a href="#/editor">Open editor</a></p>
      : <AuthForm key={route} signup={route === '#/signup'} onLogin={setUser} relaxedPasswords={sites.relaxedPasswords} />;
  } else if (['#/confirm-email', '#/resend-confirmation', '#/forgot-password', '#/reset-password'].includes(route.split('?')[0])) {
    content = <AccountRecovery key={route} route={route} relaxedPasswords={sites.relaxedPasswords} />;
  } else if (route === '#/admin-users' && sites.developmentAdmin) {
    content = <AdminUsers />;
  } else {
    content = <><h2>Page not found</h2><a href="#/">Return home</a></>;
  }

  return (
    <>
      <header>
        <h1>STATIK</h1>
        <nav aria-label="Main">
          <a href="#/">Home</a>{' '}
          <a href="#/editor">Editor</a>{' '}
          {sites.developmentAdmin && <><a href="/admin">Development admin</a>{' '}</>}
          {!loading && (user ? <button disabled={loggingOut} onClick={() => void logout()}>{loggingOut ? 'Logging out...' : 'Log out'}</button>
            : <><a href="#/login">Log in</a>{' '}<a href="#/signup">Sign up</a></>)}
        </nav>
      </header>
      <main>
        {error && <p role="alert">{error} <button onClick={() => void loadSession()}>Retry session</button></p>}
        {content}
      </main>
    </>
  );
}
