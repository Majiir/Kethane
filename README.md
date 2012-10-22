Kethane
=======

Resource mining and processing plugin for [Kerbal Space Program](http://www.kerbalspaceprogram.com/).

Forum thread: http://forum.kerbalspaceprogram.com/showthread.php/23979-Kethane-Pack
Maintainer: [Majiir](http://forum.kerbalspaceprogram.com/member.php/7556-Majiir)

Building
--------

There's currently no one-step build option. The process is as follows:

1. Build the plugin DLL. Make sure to reference the Assembly-CSharp and UnityEngine assemblies from the version of KSP you wish to target. (A plugin build targeted to one version may not work on another, even if no code changes are necessary for compatibility.)
2. Copy the part .cfg files from the repository Parts/ directory.
3. Copy any other assets from the latest public release. In particular, .wav, .mu and .mbm files are currently excluded from the repository.

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