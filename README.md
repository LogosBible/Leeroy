# Leeroy

Leeroy is a Windows service that monitors GitHub repositories for changes;
when a new commit is detected, it starts a [Jenkins](http://jenkins-ci.org)
build.

## Build Repositories

Leeroy is designed to work with a &ldquo;build repository&rdquo;&mdash;a
git repository that contains a text file with the current build number,
and one or more submodules.

Leeroy monitors a specific branch in each submodule&rsquo;s repository.
When a new commit is pushed to that branch, Leeroy creates a commit in the
build repository that updates that submodule; it also increments the
current build number. Once the commit it pushed, it makes a HTTP request
to Jenkins (or any compatible CI server) to start a build.

## GitHub API

Checking for changes and creating new commits is done through the [GitHub
API](http://developer.github.com/v3/), so Leeroy can only work with
repositories hosted at github.com or at [GitHub Enterprise](https://enterprise.github.com/).

## How To Install

1. Clone the Leeroy repository.
2. Build the code.
3. Copy the Release build output to a folder on your server.
4. Edit `Leeroy.exe.config` in that folder and ensure the credentials and
   logging settings are correct.
5. Open an Administrative Command Prompt in that folder.
6. Run `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\InstallUtil.exe
   Leeroy.exe`
7. Run `sc start Leeroy` to start the service.

## How To Configure

Leeroy loads its configuration files from a GitHub repository (and
automatically reloads them when the `master` branch in that repository is
updated). The configuration files are stored in the following JSON format:

```
{
    "buildUrl": "http://SERVER/job/JENKINS-JOB-NAME/build",
    "buildUrls": [ optional array of URLs, if a commit should start
        multiple builds ],
    "repoUrl": "git@SERVER:Build/BUILDREPO.git",
    "branch": "master", /* or branch to build */
    "submoduleBranches": { optional hash, which maps submodule
        paths to the branch to track in that submodule; the default is to
        track the branch given by 'branch' above
    }
}
```