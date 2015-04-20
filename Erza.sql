CREATE DATABASE "Erza"
  WITH OWNER = "Erza"
       ENCODING = 'UTF8'
       TABLESPACE = pg_default
       LC_COLLATE = 'Russian_Russia.1251'
       LC_CTYPE = 'Russian_Russia.1251'
       CONNECTION LIMIT = -1;

CREATE TABLE tags
(
  tag_id bigint NOT NULL DEFAULT nextval('tags2_tagid_seq'::regclass),
  tag character varying(128) NOT NULL,
  CONSTRAINT tagid PRIMARY KEY (tag_id)
)
WITH (
  OIDS=FALSE
);
ALTER TABLE tags
  OWNER TO "Erza";

-- Index: tag_index

-- DROP INDEX tag_index;

CREATE UNIQUE INDEX tag_index
  ON tags
  USING btree
  (tag COLLATE pg_catalog."default");

CREATE TABLE images
(
  image_id bigserial NOT NULL,
  is_deleted boolean DEFAULT false,
  hash character varying(32) NOT NULL,
  file_path character varying(4096),
  CONSTRAINT image_id PRIMARY KEY (image_id)
)
WITH (
  OIDS=FALSE
);
ALTER TABLE images
  OWNER TO "Erza";

-- Index: images_index

-- DROP INDEX images_index;

CREATE UNIQUE INDEX images_index
  ON images
  USING btree
  (hash COLLATE pg_catalog."default");

CREATE TABLE image_tags
(
  image_id bigint NOT NULL,
  tag_id bigint NOT NULL
)
WITH (
  OIDS=FALSE
);
ALTER TABLE image_tags
  OWNER TO "Erza";

-- Function: add_tag(character varying)

-- DROP FUNCTION add_tag(character varying);

CREATE OR REPLACE FUNCTION add_tag(par1 character varying)
  RETURNS bigint AS
$BODY$
DECLARE
    res1 bigint;
BEGIN

SELECT tag_id INTO res1 FROM   tags WHERE  tag = $1;

IF NOT FOUND THEN
   insert into tags (tag) values ($1);
   select tag_id INTO res1 from tags where tag = $1;
END IF;

RETURN res1;
END;
$BODY$
  LANGUAGE plpgsql VOLATILE
  COST 100;
ALTER FUNCTION add_tag(character varying)
  OWNER TO "Erza";

-- Function: add_image(boolean, character varying, character varying)

-- DROP FUNCTION add_image(boolean, character varying, character varying);

CREATE OR REPLACE FUNCTION add_image(
    is_deleted boolean,
    hash character varying,
    file_path character varying)
  RETURNS bigint AS
$BODY$
DECLARE
    res1 bigint;
BEGIN

SELECT image_id INTO res1 FROM images WHERE hash = $2;

IF NOT FOUND THEN
   INSERT INTO images (is_deleted, hash, file_path) VALUES ($1, $2, $3);
   SELECT image_id INTO res1 FROM images WHERE hash = $2;
END IF;

RETURN res1;
END;
$BODY$
  LANGUAGE plpgsql VOLATILE
  COST 100;
ALTER FUNCTION add_image(boolean, character varying, character varying)
  OWNER TO "Erza";
