#!/bin/bash

# =============================================================================
# Example Usage Script for NuGet Publishing
# =============================================================================
# This script demonstrates how to use the publish-nuget.sh script with the
# PinkRoosterAi.Persistify project.

set -e

readonly PROJECT_FILE="./PinkRoosterAi.Persistify/PinkRoosterAi.Persistify.csproj"
readonly VERSION="0.9.1"

echo "=== Example: Publishing PinkRoosterAi.Persistify v$VERSION ==="
echo

# Example 1: Using environment variable for API key
echo "Example 1: Using environment variable for API key"
echo "export NUGET_API_KEY=\"your-api-key-here\""
echo "./publish-nuget.sh \"$VERSION\" \"\" \"$PROJECT_FILE\""
echo

# Example 2: Using API key as argument
echo "Example 2: Using API key as argument"
echo "./publish-nuget.sh \"$VERSION\" \"your-api-key-here\" \"$PROJECT_FILE\""
echo

# Example 3: Dry run - show what would be executed
echo "Example 3: Testing argument validation (dry run)"
echo "./publish-nuget.sh \"$VERSION\" --help"
echo

echo "To actually publish, set your NUGET_API_KEY and run:"
echo "  NUGET_API_KEY=\"your-key\" ./publish-nuget.sh \"$VERSION\" \"\" \"$PROJECT_FILE\""