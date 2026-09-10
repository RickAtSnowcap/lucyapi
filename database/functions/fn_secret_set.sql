-- fn_secret_set.sql
-- Upserts a secret. Inserts or updates encrypted_value if key already exists.

CREATE OR REPLACE FUNCTION lucyapi.fn_secret_set(p_user_id INT, p_key TEXT, p_encrypted_value BYTEA)
RETURNS TABLE(secret_id INT, key TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
#variable_conflict use_column
BEGIN
    RETURN QUERY
    INSERT INTO public.secrets (user_id, key, encrypted_value)
    VALUES (p_user_id, p_key, p_encrypted_value)
    ON CONFLICT (user_id, key) DO UPDATE
       SET encrypted_value = EXCLUDED.encrypted_value,
           updated_at = NOW()
    RETURNING secrets.secret_id, secrets.key;
END;
$proc$;
