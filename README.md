Kethane
=======

Resource mining and processing plugin for [Kerbal Space Program](http://www.kerbalspaceprogram.com/).

Forum thread: [Kethane Pack](http://forum.kerbalspaceprogram.com/showthread.php/23979-Kethane-Pack)  
Maintainer: [Majiir](http://forum.kerbalspaceprogram.com/member.php/7556-Majiir)

Building
--------

There's currently no one-step build option. The process is as follows:

1. Build the plugin DLL with as target "Release" in Visual Studio. Make sure to reference the Assembly-CSharp and UnityEngine assemblies from the version of KSP you wish to target. (A plugin build targeted to one version may not work on another, even if no code changes are necessary for compatibility.)
2. Download the latest public Kethane release, copy the "Kethane" folder into the folder containing this readme and rename it to KethaneReleaseFolder.
3. Run create_mod_folder.bat

Running
-------

After building the mod, it's easy to run it:

1. Copy the folder ModFolder created after building to KSP's GameData folder.
2. Run KSP.exe

Reporting Issues
----------------

Please provide as much detail as possible when reporting an issue. That said, if you encounter an issue and aren't able to pin down the cause, post it and explain what you've tried so far. Some bugs are difficult to reproduce, and we need to know about them anyway.

Feature suggestions are also welcome. However, don't be surprised if these issues are closed. The project will be expanding, but some features are out of the intended scope and won't be included.

Coding and Pull Request Guidelines
----------------------------------

- The project follows ["A successful Git branching model"](http://nvie.com/posts/a-successful-git-branching-model/) with some minor modifications. Namely, before a release branch may be merged into `master`, any changes on `master` must have been merged into the release branch (or other branches upstream).
- Pull requests should be tested before submission.
- Keep your commits clean. Before committing, check your changes and make sure you're only committing the absolute minimum changes necessary. In particular, avoid committing changes to the .csproj file unless you're absolutely sure that those changes should be included in the repository.
- Don't commit binary files, including DLLs and models.
- No merges should be included in pull requests unless the pull request's purpose is a merge.
- No tabs, please. Use four spaces instead.
- No trailing whitespaces.
- No CRLF line endings, LF only, put your gits 'core.autocrlf' on 'true'.
- No 80 column limit or 'weird' midstatement newlines.

Keep in mind that some pull requests will be rejected at first, but with changes, may be accepted again. Don't be offended if your pull is rejected; it's just an effort to maintain a consistent code base and feature growth.

Model and Part Asset Submission Guidelines
------------------------------------------

Part assets should be submitted by private message to [Majiir](http://forum.kerbalspaceprogram.com/member.php/7556-Majiir) for review.

The following contents should be packaged in a .zip or .rar archive:

- Model to be submitted as a .obj or .FBX, must include low-poly convex node_collider mesh
    - Meshes of appropriate resolution/polycount (<1000 tris small part, <2000 tris large part recommended)
    - Objects named properly (there's no strict convention, but make sure they're clearly named; e.g. fuelTank_geo, geo_engine, enginePoly, etc.)
    - All transformations must be cleared/frozen
    - No n-gons (five or greater sided polygons)
    - UVs mapped to 0-1 space, not overlapping (you can overlap UV shells, just not UVs of the same shell)
- Texture map and Normal map (optional) to be submitted as a .png or .jpeg
    - Appropriate resolution for part size, not to exceed 1024
    - AO baked textures recommended
- Notes in .txt file (optional)

Part submissions may be returned for any of the following reasons, in approximately this order:

- Part doesn't fall within the scope of the plugin
- Assets submission doesn't meet above guidelines
- Part concept doesn't fit the art style of the plugin
- Assets need further work to improve quality

Our goal is to help you submit your parts, but at the same time, contributions can't add significantly to our workload. These guidelines are here to ensure a high standard of quality and to make the process as painless as possible for everyone involved.