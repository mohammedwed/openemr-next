-- DEV ONLY. Registers the AuthFacade OAuth client and keeps its scope in sync.
-- @CLIENT_ID and @CLIENT_SECRET are set by seed-auth-client.sh.
INSERT INTO oauth_clients
  (client_id, client_name, client_role, client_secret, redirect_uri, grant_types,
   scope, user_id, site_id, is_confidential, is_enabled,
   skip_ehr_launch_authorization_flow, dsi_type, register_date)
SELECT
  @CLIENT_ID, 'AuthFacade', 'user', @CLIENT_SECRET,
  'http://localhost:7128/callback', 'authorization_code',
  'openid profile email offline_access', NULL, 'default', 1, 1,
  0, 0, NOW()
FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM oauth_clients WHERE client_id = @CLIENT_ID);

UPDATE oauth_clients SET scope = 'openid profile email offline_access' WHERE client_id = @CLIENT_ID;