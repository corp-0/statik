import { useState } from 'react';
import type { FormEvent } from 'react';
import { confirmEmail, resetPassword, forgotPassword, resendConfirmation, errorMessage } from './api';

export default function AccountRecovery({ route, relaxedPasswords }: { route: string; relaxedPasswords: boolean }) {
  const [path, query] = route.slice(2).split('?');
  const parameters = new URLSearchParams(query);
  const token = parameters.get('token');
  const tokenEmail = parameters.get('email');
  const confirming = path === 'confirm-email';
  const resetting = path === 'reset-password';
  const titles: Record<string, string> = {
    'confirm-email': 'Confirm email',
    'reset-password': 'Reset password',
    'forgot-password': 'Forgot password',
  };
  const title = titles[path] ?? 'Resend confirmation';
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    setError('');
    setBusy(true);
    try {
      if (confirming || resetting) {
        if (!token || !tokenEmail) throw new Error('This link is incomplete. Request a new email below.');
        const confirmation = { email: tokenEmail, token };
        if (resetting) {
          await resetPassword({ ...confirmation, password: String(data.get('password') ?? '') });
        } else {
          await confirmEmail(confirmation);
        }
      } else {
        const email = { email: String(data.get('email') ?? '') };
        if (path === 'forgot-password') {
          await forgotPassword(email);
        } else {
          await resendConfirmation(email);
        }
      }
      if (confirming) {
        setMessage('Email confirmed. You can now log in.');
      } else if (resetting) {
        setMessage('Password reset. Log in with your new password.');
      } else {
        setMessage('If the account is eligible, an email has been sent. Check your inbox.');
      }
      if (confirming || resetting) window.history.replaceState(null, '', `/#/${path}`);
    } catch (error) {
      setError(errorMessage(error));
    } finally {
      setBusy(false);
    }
  }

  return <section>
    <h2>{title}</h2>
    {error && <p role="alert">{error}</p>}
    {message ? <p role="status">{message}</p> : <form onSubmit={submit}>
      <fieldset disabled={busy}>
        <legend>{title}</legend>
        {confirming || resetting ? <p>{tokenEmail}</p> : <p>
          <label htmlFor="recovery-email">Email</label><br />
          <input id="recovery-email" name="email" type="email" autoComplete="email" required maxLength={256} />
        </p>}
        {(confirming || resetting) && (!token || !tokenEmail) && <p role="alert">This link is incomplete. Request a new email below.</p>}
        {resetting && <p>
          <label htmlFor="new-password">New password</label><br />
          <input id="new-password" name="password" type="password" autoComplete="new-password" required minLength={relaxedPasswords ? 1 : 6} />
        </p>}
        {resetting && <p>{relaxedPasswords ? 'Any nonempty password is accepted in development.'
          : 'At least 6 characters, including uppercase, lowercase, a number, and a symbol.'}</p>}
        <button disabled={(confirming || resetting) && (!token || !tokenEmail)}>{busy ? 'Please wait...' : title}</button>
      </fieldset>
    </form>}
    <p><a href="#/login">Log in</a>{' '}
      {resetting ? <a href="#/forgot-password">Request a new password reset</a> : <a href="#/resend-confirmation">Resend confirmation</a>}
    </p>
  </section>;
}
