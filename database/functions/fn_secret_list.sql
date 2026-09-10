-- fn_secret_list.sql
-- Returns all secrets for a user (keys only, no encrypted_value).

CREATE OR REPLACE FUNCTION lucyapi.fn_secret_list(p_user_id INT)
RETURNS TABLE(secret_id INT, key TEXT, created_at TIMESTAMPTZ, updated_at TIMESTAMPTZ)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
#variable_conflict use_column
BEGIN
    RETURN QUERY
    SELECT s.secret_id,
           s.key,
           s.created_at,
           s.updated_at
      FROM public.secrets s
     WHERE s.user_id = p_user_id
     ORDER BY s.key;
END;
$proc$;
