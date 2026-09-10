-- fn_secret_get.sql
-- Returns a single secret by user_id + key (includes encrypted_value).

CREATE OR REPLACE FUNCTION lucyapi.fn_secret_get(p_user_id INT, p_key TEXT)
RETURNS TABLE(secret_id INT, key TEXT, encrypted_value BYTEA, created_at TIMESTAMPTZ, updated_at TIMESTAMPTZ)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
#variable_conflict use_column
BEGIN
    RETURN QUERY
    SELECT s.secret_id,
           s.key,
           s.encrypted_value,
           s.created_at,
           s.updated_at
      FROM public.secrets s
     WHERE s.user_id = p_user_id
       AND s.key = p_key;
END;
$proc$;
