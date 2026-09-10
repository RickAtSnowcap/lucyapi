-- fn_image_delete_batch.sql
-- Bulk deletes image records by ID array. Returns count deleted.
-- Caller is responsible for deleting files from disk first.

CREATE OR REPLACE FUNCTION lucyapi.fn_image_delete_batch(p_image_ids INT[])
RETURNS INT
LANGUAGE plpgsql
SECURITY INVOKER
AS $proc$
DECLARE
    v_count INT;
BEGIN
    DELETE FROM images WHERE image_id = ANY(p_image_ids);
    GET DIAGNOSTICS v_count = ROW_COUNT;
    RETURN v_count;
END;
$proc$;
