// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (C) 2026 The Prizm contributors

namespace Prizm.Core.Tests.ThirdParty;

/// <summary>
/// Third-party test files that may not be committed (unclear licence) live in the repository's
/// local/ folder, ignored by Git. Tests that need one are skipped when it is missing.
/// </summary>
internal static class LocalFile
{
    public static string Path(string relativePath) =>
        System.IO.Path.Combine(RepositoryRoot(), "local", relativePath);

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "Prizm.sln")))
            {
                return directory.FullName;
            }
        }

        return AppContext.BaseDirectory;
    }
}

/// <summary>A theory skipped, with instructions, when its local/ file is missing.</summary>
internal sealed class LocalFileTheoryAttribute : TheoryAttribute
{
    public LocalFileTheoryAttribute(string relativePath, string source)
    {
        if (!File.Exists(LocalFile.Path(relativePath)))
        {
            Skip = $"Missing local/{relativePath} (see {source}).";
        }
    }
}
