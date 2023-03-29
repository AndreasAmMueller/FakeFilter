-- Initializing the database
-- 2023-03-28 Andreas Müller

create table [hosts] (
	[id] int identity(1, 1),
	[name] nvarchar(100) not null,
	constraint [pk_hosts]
		primary key ([id]),
	constraint [uq_hosts_name]
		unique ([name])
);

create table [providers] (
	[id] int identity(1, 1),
	[name] nvarchar(100) not null,
	constraint [pk_providers]
		primary key ([id]),
	constraint [uq_providers_name]
		unique ([name])
);

create table [host_provider_mappings] (
	[host_id] int not null,
	[provider_id] int not null,
	constraint [pk_host_provider_mappings]
		primary key ([host_id], [provider_id]),
	constraint [fk_host_provider_mappings_host_id]
		foreign key ([host_id])
		references [hosts] ([id])
		on delete cascade,
	constraint [fk_host_provider_mappings_provider_id]
		foreign key ([provider_id])
		references [providers] ([id])
		on delete cascade
);

create table [changelog] (
	[id] int identity(1, 1),
	[timestamp] datetime2(0) not null,
	[duration] time not null,
	constraint [pk_changelog]
		primary key ([id])
);

create table [changelog_entries] (
	[id] nvarchar(40) not null,
	[changelog_id] int not null,
	[type] tinyint not null,
	[action] tinyint not null,
	[domain] nvarchar(100) not null,
	constraint [pk_changelog_entries]
		primary key ([id]),
	constraint [fk_changelog_entries_changelog_id]
		foreign key ([changelog_id])
		references [changelog] ([id])
		on delete cascade
);