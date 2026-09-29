-- fn_oauth_user_for_login.sql
-- Fetches the password hash for the /oauth/authorize sign-in page (verified in the app with
-- the same PasswordHasher the admin login uses).

CREATE OR REPLACE FUNCTION lucyapi.fn_oauth_user_for_login(p_username TEXT)
RETURNS TABLE(user_id INTEGER, name TEXT, password_hash TEXT)
LANGUAGE plpgsql
AS $proc$
BEGIN
    RETURN QUERY
    SELECT u.user_id, u.name::TEXT, u.password_hash::TEXT
      FROM public.users u
     WHERE u.username = p_username
       AND u.password_hash IS NOT NULL;
END;
$proc$;
