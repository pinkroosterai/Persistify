#!/bin/bash

# =============================================================================
# Example Usage Script for NuGet Publishing with Git Integration
# =============================================================================
# This script demonstrates how to use the enhanced publish-nuget.sh script with
# automatic git push and tagging functionality.

set -e

readonly PROJECT_FILE="./PinkRoosterAi.Persistify/PinkRoosterAi.Persistify.csproj"
readonly VERSION="1.0.3"

echo "=== Enhanced NuGet Publishing with Git Integration ==="
echo "Publishing PinkRoosterAi.Persistify v$VERSION with automatic git operations"
echo

echo "🚀 Complete Automated Workflow:"
echo "  1. Build project in Release mode"
echo "  2. Create NuGet package with README validation"
echo "  3. Publish package to NuGet"
echo "  4. Create git tag v$VERSION automatically"
echo "  5. Push changes and tags to remote repository"
echo

# Example 1: Using environment variable for API key (recommended)
echo "Example 1: Complete automation with environment variable"
echo "export NUGET_API_KEY=\"your-api-key-here\""
echo "./publish-nuget.sh \"$VERSION\" \"\" \"$PROJECT_FILE\""
echo "  → Builds, publishes, tags as v$VERSION, and pushes to git automatically"
echo

# Example 2: Using API key as argument
echo "Example 2: Complete automation with API key argument"
echo "./publish-nuget.sh \"$VERSION\" \"your-api-key-here\" \"$PROJECT_FILE\""
echo "  → Same automated workflow with API key as parameter"
echo

# Example 3: What happens with dummy key
echo "Example 3: Testing with dummy key (git operations still work)"
echo "NUGET_API_KEY=\"dummy-key\" ./publish-nuget.sh \"$VERSION-test\" \"\" \"$PROJECT_FILE\""
echo "  → Build succeeds, NuGet publish fails, but git tag and push succeed"
echo

echo "📋 Prerequisites:"
echo "  ✓ Git repository with 'origin' remote configured"
echo "  ✓ Valid NuGet API key (from nuget.org)"
echo "  ✓ Clean working directory (uncommitted changes will be warned about)"
echo

echo "🎯 To publish version $VERSION:"
echo "  1. Ensure all changes are committed"
echo "  2. Set your NuGet API key: export NUGET_API_KEY=\"your-key\""
echo "  3. Run: ./publish-nuget.sh \"$VERSION\" \"\" \"$PROJECT_FILE\""
echo "  4. Script handles everything: build → publish → tag → push"
echo

echo "💡 The script now automatically handles the complete release workflow!"
echo "   No manual git tagging or pushing required."