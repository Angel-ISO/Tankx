/**
 * Environment por defecto (desarrollo).
 *
 * Angular NO lee archivos `.env` automáticamente. Los valores que el
 * frontend necesita en tiempo de ejecución viven aquí y se reemplazan
 * por `environment.prod.ts` durante la build de producción gracias al
 * `fileReplacements` configurado en `angular.json`.
 *
 * NEVER exponer aquí claves secretas (SUPABASE_SECRET_KEY, tokens de
 * servicio, etc.): todo este archivo se empaqueta y se envía al
 * navegador. Solo claves publicable/safe-to-expose.
 */
export const environment = {
  production: false,

  // Base del backend ASP.NET (sin barra final).
  // Coincide con `applicationUrl` de backend/Properties/launchSettings.json.
  backendUrl: 'http://localhost:5074',

  gameHubUrl: 'http://localhost:5074/hubs/game',

  // Broker MQTT (HiveMQ Cloud) para eventos de baja criticidad (kill feed).
  // Credenciales compartidas del lab 6; el frontend genera un clientId único
  // por pestaña para no expulsar a otras sesiones con el mismo id.
  mqtt: {
    url: 'wss://b502948fc30642918d835f65aea64e18.s1.eu.hivemq.cloud:8884/mqtt',
    username: 'hivemq.webclient.1784671506396',
    password: 'uePLDt&Aj214<I8B>qb$',
    topic: 'tankx/telemetry',
  },
  supabase: {
    url: 'https://rdzxrpxizyiwabakczrn.supabase.co',
    publishableKey: 'sb_publishable_qmZRB0f9Fbr5r0W8RTELPQ_CGOQ26ee',
    jwksUrl: 'https://rdzxrpxizyiwabakczrn.supabase.co/auth/v1/.well-known/jwks.json',
  },
};

export type Environment = typeof environment;
