-- fn_user_get_by_username.sql
-- Returns a user by username, including the password hash (admin login only).

CREATE OR REPLACE FUNCTION lucyapi.fn_user_get_by_username(p_username TEXT)
RETURNS TABLE(user_id INT, name TEXT, username TEXT, password_hash TEXT, email TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT u.user_id, u.name, u.username, u.password_hash, u.email
      FROM public.users u
     WHERE u.username = p_username;
END;
$proc$;
