# Twenty Tons — everyday commands. Unity itself is driven from the Hub; these cover everything else.

.PHONY: check sandbox sandbox-publish sandbox-serve world

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

# Build the world from OpenStreetMap: download the route corridor, cut it into kilometre chunks of
# grey-box meshes, and make the scenes in Unity (the editor must be closed, or use the menu
# Twenty Tons > Build Corridor01 grey box instead of the last step). The meshes are not in git.
world:
	./tools/osm/fetch.sh route.osm
	python3 tools/osm/greybox.py route.osm Assets/World/Corridor01 300
	unity run . -- -executeMethod TwentyTons.EditorTools.GreyboxSceneBuilder.Build
