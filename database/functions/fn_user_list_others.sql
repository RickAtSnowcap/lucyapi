-- fn_user_list_others.sql
-- Lists every user except the caller (share-with picker). Never returns password hashes.

CREATE OR REPLACE FUNCTION lucyapi.fn_user_list_others(p_user_id INT)
RETURNS TABLE(user_id INT, name TEXT, username TEXT, email TEXT)
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
BEGIN
    RETURN QUERY
    SELECT u.user_id, u.name, u.username, u.email
      FROM public.users u
     WHERE u.user_id <> p_user_id
     ORDER BY u.name;
END;
$proc$;
