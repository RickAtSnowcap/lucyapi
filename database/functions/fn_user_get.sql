-- fn_user_get.sql
-- Returns a user by id, including the password hash (admin /auth/me and change-password).

CREATE OR REPLACE FUNCTION lucyapi.fn_user_get(p_user_id INT)
RETURNS TABLE(user_id INT, name TEXT, username TEXT, password_hash TEXT, email TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT u.user_id, u.name, u.username, u.password_hash, u.email
      FROM public.users u
     WHERE u.user_id = p_user_id;
END;
$proc$;
