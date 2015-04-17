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
