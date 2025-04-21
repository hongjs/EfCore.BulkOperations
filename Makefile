build:
	dotnet build ${PWD}/EfCore.BulkOperations.sln -c Release

restore:
	dotnet restore

format:
	dotnet format ${PWD}/EfCore.BulkOperations.sln --verbosity=normal

test:
	dotnet test ${PWD}/EfCore.BulkOperations.sln

migrate-db:
	dotnet ef migrations add ${name} --project EfCore.BulkOperations.API --context ApplicationDbContext

remove-migrate-db:
	dotnet ef migrations remove --project EfCore.BulkOperations.API --context ApplicationDbContext

update-db:
	dotnet ef database update --project EfCore.BulkOperations.API --context ApplicationDbContext