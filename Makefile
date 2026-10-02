# Twenty Tons — everyday commands. Unity itself is driven from the Hub; these cover everything else.

.PHONY: check sandbox sandbox-publish sandbox-serve

# Compile every script with Mono against the UnityEngine stub and run the tests (no Unity needed).
check:
	./tools/check.sh

# Run the browser sandbox with the .NET dev server (prints a http://localhost:PORT to open).
sandbox:
	cd sandbox && dotnet run

# Publish the sandbox as plain static files into sandbox/out/wwwroot.
sandbox-publish:
	cd sandbox && dotnet publish -c Release -o out

# Serve the published files on http://localhost:8080 with Python's built-in server.
sandbox-serve: sandbox-publish
	python3 -m http.server -d sandbox/out/wwwroot 8080
