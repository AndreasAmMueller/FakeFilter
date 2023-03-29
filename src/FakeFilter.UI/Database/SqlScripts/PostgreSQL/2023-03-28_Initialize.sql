-- Initializing the database
-- 2023-03-28 Andreas Müller

create table "hosts" (
	"id" serial primary key,
	"name" varchar(100) not null,
	constraint "uq_hosts_name"
		unique ("name")
);

create table "providers" (
	"id" serial primary key,
	"name" varchar(100) not null,
	constraint "uq_providers_name"
		unique ("name")
);

create table "host_provider_mappings" (
	"host_id" int not null,
	"provider_id" int not null,
	constraint "pk_host_provider_mappings"
		primary key ("host_id", "provider_id"),
	constraint "fk_host_provider_mappings_host_id"
		foreign key ("host_id")
		references "hosts" ("id")
		on delete cascade,
	constraint "fk_host_provider_mappings_provider_id"
		foreign key ("provider_id")
		references "providers" ("id")
		on delete cascade
);

create table "changelog" (
	"id" serial primary key,
	"timestamp" timestamp(0) not null,
	"duration" interval not null
);

create table "changelog_entries" (
	"id" varchar(40) primary key,
	"changelog_id" int not null,
	"type" smallint not null,
	"action" smallint not null,
	"domain" varchar(100) not null,
	constraint "fk_changelog_entries_changelog_id"
		foreign key ("changelog_id")
		references "changelog" ("id")
		on delete cascade
);