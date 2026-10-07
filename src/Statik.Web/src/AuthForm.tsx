import { useState } from 'react';
import type { FormEvent } from 'react';
import { signup as createAccount, login, getCurrentUser, errorMessage } from './api';
import type { User } from './api';

export default function AuthForm({ signup, onLogin, relaxedPasswords }: { signup: boolean; onLogin: (user: User) => void; relaxedPasswords: boolean }) {
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [registered, setRegistered] = useState(false);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    setBusy(true);
    setError('');
    try {
      const credentials = {
        email: String(data.get('email') ?? ''),
        password: String(data.get('password') ?? ''),
      };
      if (signup) {
        await createAccount({
          ...credentials,
          username: String(data.get('username') ?? ''),
          displayName: String(data.get('displayName') ?? ''),
        });
        setRegistered(true);
      } else {
        await login(credentials);
        onLogin(await getCurrentUser());
        window.location.hash = '/editor';
      }
    } catch (error) {
      setError(errorMessage(error));
    } finally {
      setBusy(false);
    }
  }

  if (registered) return <section>
    <h2>Check your email</h2>
    <p role="status">If this address can be registered, a confirmation link has been sent. Confirm your email before logging in.</p>
    <p><a href="#/login">Log in</a>{' '}<a href="#/resend-confirmation">Resend confirmation</a></p>
  </section>;

  const submitLabel = signup ? 'Create account' : 'Log in';

  return (
    <section>
      <h2>{signup ? 'Sign up' : 'Log in'}</h2>
      {error && <p role="alert">{error}</p>}
      <form onSubmit={submit}>
        <fieldset disabled={busy}>
          <legend>Account details</legend>
          {signup && <p>
            <label htmlFor="username">Public username</label><br />
            <input id="username" name="username" autoComplete="username" required maxLength={25}
              pattern={signup ? String.raw`[a-zA-Z0-9_\-]+` : undefined} aria-describedby={signup ? 'username-help' : undefined} />
          </p>}
          {signup && <>
            <p id="username-help">Use 1-25 letters, numbers, hyphens, or underscores.</p>
            <p>
              <label htmlFor="displayName">Display name</label><br />
              <input id="displayName" name="displayName" autoComplete="nickname" required maxLength={25} />
            </p>
          </>}
          <p>
            <label htmlFor="email">Email</label><br />
            <input id="email" name="email" type="email" autoComplete={signup ? 'email' : 'username'} required maxLength={256} />
          </p>
          <p>
            <label htmlFor="password">Password</label><br />
            <input id="password" name="password" type="password" required
              autoComplete={signup ? 'new-password' : 'current-password'}
              minLength={signup ? (relaxedPasswords ? 1 : 6) : undefined} aria-describedby={signup ? 'password-help' : undefined} />
          </p>
          {signup && <p id="password-help">{relaxedPasswords ? 'Any nonempty password is accepted in development.'
            : 'At least 6 characters, including uppercase, lowercase, a number, and a symbol.'}</p>}
          <button type="submit">{busy ? 'Please wait...' : submitLabel}</button>
        </fieldset>
      </form>
      <p><a href={signup ? '#/login' : '#/signup'}>{signup ? 'Already have an account? Log in' : 'Create an account'}</a></p>
      {!signup && <p><a href="#/forgot-password">Forgot password?</a>{' '}<a href="#/resend-confirmation">Resend confirmation</a></p>}
    </section>
  );
}
