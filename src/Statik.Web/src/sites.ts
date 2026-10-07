export type Sites = { publicOrigin: string; appOrigin: string; developmentAdmin: boolean; relaxedPasswords: boolean };

export const sites: Sites = {
  publicOrigin: import.meta.env.VITE_PUBLIC_ORIGIN,
  appOrigin: import.meta.env.VITE_APP_ORIGIN,
  developmentAdmin: import.meta.env.VITE_DEVELOPMENT_ADMIN === 'true',
  relaxedPasswords: import.meta.env.VITE_RELAXED_PASSWORDS === 'true',
};
