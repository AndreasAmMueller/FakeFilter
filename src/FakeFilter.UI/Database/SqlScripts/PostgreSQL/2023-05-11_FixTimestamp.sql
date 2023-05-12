-- Fixing timestamps to use timezone
-- 2023-05-211 Andreas Müller

alter table "changelog"
	alter column "timestamp" type timestamp with time zone
;
