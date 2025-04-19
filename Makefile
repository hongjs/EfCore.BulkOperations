build:
	dotnet build ${PWD}/EfCore.BulkOperations.sln -c Release

restore:
	dotnet restore

format:
	dotnet format ${PWD}/EfCore.BulkOperations.sln --verbosity=normal

test:
	dotnet test ${PWD}/EfCore.BulkOperations.sln