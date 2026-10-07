import { useEffect, useState } from 'react';
import type { FormEvent } from 'react';
import { getAdminUsers, createAdminUser, updateAdminUser, deleteAdminUser, errorMessage } from './api';
import type { AdminUser, CreateAdminUserRequest } from './api';

const emptyUser = { id: '', userName: '', displayName: '', email: '', about: '', emailConfirmed: true };

export default function AdminUsers() {
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [selected, setSelected] = useState<AdminUser>(emptyUser);
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  async function load() {
    try { setUsers(await getAdminUsers()); }
    catch (error) { setError(errorMessage(error)); }
  }
  useEffect(() => { void load(); }, []);

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true); setError(''); setMessage('');
    try {
      const request: CreateAdminUserRequest = {
        userName: selected.userName ?? '',
        displayName: selected.displayName,
        email: selected.email ?? '',
        about: selected.about,
        emailConfirmed: selected.emailConfirmed,
        password,
      };
      if (selected.id) {
        await updateAdminUser(selected.id, request);
      } else {
        await createAdminUser(request);
      }
      setPassword(''); setSelected(emptyUser); setMessage('User saved. No email was sent.');
      await load();
    } catch (error) { setError(errorMessage(error)); }
    finally { setBusy(false); }
  }

  async function remove() {
    if (!window.confirm(`Delete ${selected.userName} and their page?`)) return;
    setBusy(true); setError(''); setMessage('');
    try {
      await deleteAdminUser(selected.id);
      setSelected(emptyUser); setPassword(''); setMessage('User deleted.'); await load();
    } catch (error) { setError(errorMessage(error)); }
    finally { setBusy(false); }
  }

  return <section>
    <h2>Development user admin</h2>
    <p>Local development only. You can confirm an email here without sending mail.</p>
    <p><a href="/admin">Model admin</a></p>
    {error && <p role="alert">{error}</p>}
    <p role="status">{message}</p>
    <button disabled={busy} onClick={() => { setSelected(emptyUser); setPassword(''); }}>New user</button>
    <ul>{users.map(user => <li key={user.id}>
      <button disabled={busy} onClick={() => { setSelected(user); setPassword(''); setMessage(''); }}>{user.userName}</button>
      {' '}{user.email ?? 'No email'} - {user.emailConfirmed ? 'Confirmed' : 'Unconfirmed'}
    </li>)}</ul>
    <form onSubmit={save}>
      <fieldset disabled={busy}>
        <legend>{selected.id ? `Edit ${selected.userName}` : 'Create user'}</legend>
        <p><label htmlFor="admin-username">Public username</label><br />
          <input id="admin-username" required maxLength={25} value={selected.userName ?? ''} onChange={event => setSelected({ ...selected, userName: event.target.value })} /></p>
        <p><label htmlFor="admin-name">Display name</label><br />
          <input id="admin-name" required maxLength={25} value={selected.displayName} onChange={event => setSelected({ ...selected, displayName: event.target.value })} /></p>
        <p><label htmlFor="admin-email">Email</label><br />
          <input id="admin-email" type="email" required maxLength={256} value={selected.email ?? ''} onChange={event => setSelected({ ...selected, email: event.target.value })} /></p>
        <p><label htmlFor="admin-about">About</label><br />
          <textarea id="admin-about" maxLength={200} value={selected.about} onChange={event => setSelected({ ...selected, about: event.target.value })} /></p>
        <p><label><input type="checkbox" checked={selected.emailConfirmed} onChange={event => setSelected({ ...selected, emailConfirmed: event.target.checked })} /> Email confirmed</label></p>
        <p><label htmlFor="admin-password">{selected.id ? 'New password (leave blank to keep current)' : 'Password'}</label><br />
          <input id="admin-password" type="password" autoComplete="new-password" required={!selected.id} value={password} onChange={event => setPassword(event.target.value)} /></p>
        <button>{busy ? 'Please wait...' : 'Save user'}</button>{' '}
        {selected.id && <button type="button" onClick={() => void remove()}>Delete user</button>}
      </fieldset>
    </form>
  </section>;
}
