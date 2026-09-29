-- fn_user_set_password_hash.sql
-- Stores a new password hash for a user. Returns TRUE if the user exists.

CREATE OR REPLACE FUNCTION lucyapi.fn_user_set_password_hash(p_user_id INT, p_password_hash TEXT)
RETURNS BOOLEAN
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    UPDATE public.users
       SET password_hash = p_password_hash
     WHERE user_id = p_user_id;
    RETURN FOUND;
END;
$proc$;
