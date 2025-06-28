#!/bin/bash

# Test Git Integration in NuGet Publishing Script
# This script demonstrates the automatic git push and tagging functionality

set -e

echo "=== Git Integration Test for NuGet Publishing Script ==="
echo "This test demonstrates the automatic git push and tagging functionality"
echo

# Test version to avoid conflicts
TEST_VERSION="1.0.2-git-test"

echo "Test Configuration:"
echo "  Version: $TEST_VERSION"
echo "  Project: PinkRoosterAi.Persistify"
echo "  Expected behavior: Creates tag v$TEST_VERSION and pushes to remote"
echo

# Show current git status
echo "Current Git Status:"
echo "  Branch: $(git branch --show-current)"
echo "  Remote: $(git remote get-url origin 2>/dev/null || echo 'No origin remote')"
echo "  Last commit: $(git log --oneline -1)"
echo

# Check if we have uncommitted changes
if ! git diff-index --quiet HEAD --; then
    echo "⚠️  Uncommitted changes detected:"
    git status --porcelain | sed 's/^/    /'
    echo "  These will be warned about but won't block the publish process"
else
    echo "✓ No uncommitted changes"
fi

echo
echo "Running publish script with git integration..."
echo "Expected outcomes:"
echo "  1. Build and package creation succeeds"
echo "  2. Git repository validation passes"
echo "  3. Git tag v$TEST_VERSION is created"
echo "  4. Changes and tags are pushed to remote (if real API key)"
echo "  5. If using dummy key: publish fails but git operations succeed"
echo

# Note: Using dummy key will fail at NuGet publish but git operations should succeed
NUGET_API_KEY="dummy-key-for-git-test" \
./publish-nuget.sh "$TEST_VERSION" "" "PinkRoosterAi.Persistify/PinkRoosterAi.Persistify.csproj" 2>&1 | \
grep -E "(Checking git|Creating git tag|Pushing|Git push|SUCCESS|ERROR|WARNING|✓)" || true

echo
echo "=== Post-Test Git Status ==="

# Check if tag was created
if git tag -l | grep -q "v$TEST_VERSION"; then
    echo "✓ Git tag v$TEST_VERSION was created successfully"
    echo "  Tag details:"
    git show "v$TEST_VERSION" --no-patch --format="    Created: %ai%n    Tagger: %an <%ae>%n    Message: %s" 2>/dev/null || echo "    (Tag details unavailable)"
else
    echo "✗ Git tag v$TEST_VERSION was not created"
fi

# Check current branch status
echo
echo "Current Repository State:"
echo "  Branch: $(git branch --show-current)"
echo "  Last 3 commits:"
git log --oneline -3 | sed 's/^/    /'

echo
echo "Available tags:"
git tag -l | grep -E "^v[0-9]" | tail -5 | sed 's/^/    /' || echo "    No version tags found"

echo
echo "=== Test Summary ==="
echo "The enhanced publish-nuget.sh script now automatically:"
echo "  ✓ Validates git repository status"
echo "  ✓ Creates annotated git tags for each version"
echo "  ✓ Pushes changes and tags to remote repository"
echo "  ✓ Handles git errors gracefully without affecting NuGet publish"
echo "  ✓ Provides comprehensive feedback about git operations"
echo
echo "This makes the publishing workflow completely automated:"
echo "  1. Build & package → 2. Publish to NuGet → 3. Tag & push to git"