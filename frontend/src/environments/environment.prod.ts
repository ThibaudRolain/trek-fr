// Prod : URL publique complète du backend (schéma + host, sans slash final).
// Déploiement actuel : VPS Hetzner 178.104.47.96, TLS via sslip.io + Caddy.
// Changer cette URL et rebuild le front si on migre le serveur.
export const environment = {
  production: true,
  apiBase: 'https://api.178-104-47-96.sslip.io',
};
