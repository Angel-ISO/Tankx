/**
 * Environment de producción.
 *
 * Reemplaza a `environment.ts` durante `ng build --configuration production`
 * mediante `fileReplacements` en `angular.json`. Sustituye las URLs por
 * las de tu despliegue real antes de publicar.
 */
export const environment = {
  production: true,

  // TODO: reemplazar por la URL real del backend en producción.
  backendUrl: 'https://tankx.runasp.net',
  gameHubUrl: 'https://tankx.runasp.net/hubs/game',

  mqtt: {
    url: 'wss://b502948fc30642918d835f65aea64e18.s1.eu.hivemq.cloud:8884/mqtt',
    username: 'forbackend',
    password: 'forbackend',
    topic: 'tankx/telemetry',
  },

  // Supabase public browser configuration. Secret/service-role keys belong
  // only in a protected backend environment.
  supabase: {
    url: 'https://rdzxrpxizyiwabakczrn.supabase.co',
    publishableKey: 'sb_publishable_qmZRB0f9Fbr5r0W8RTELPQ_CGOQ26ee',
    jwksUrl: 'https://rdzxrpxizyiwabakczrn.supabase.co/auth/v1/.well-known/jwks.json',
  },
};

export type Environment = typeof environment;
