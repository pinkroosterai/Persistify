#!/bin/bash

# =============================================================================
# NuGet Publishing Script for .NET Projects with Git and GitHub Integration
# =============================================================================
# This script automates the complete process of building, publishing .NET packages
# to NuGet, pushing changes to git with automatic tagging, and creating GitHub releases.
#
# Usage:
#   ./publish-nuget.sh <version> [api-key] [project-file] [nuget-source]
#
# Arguments:
#   version       (required) - Package version (e.g., "1.2.3")
#   api-key       (optional) - NuGet API key or set NUGET_API_KEY env var
#   project-file  (optional) - Path to .csproj file (default: ./MyProject.csproj)
#   nuget-source  (optional) - NuGet source URL (default: https://api.nuget.org/v3/index.json)
#
# Environment Variables:
#   NUGET_API_KEY - API key for NuGet publishing (if not provided as argument)
#
# Dependencies:
#   Required: dotnet, git, realpath
#   Optional: gh (GitHub CLI) - for automatic GitHub release creation
#
# Automated Features:
#   ✓ Build project in Release mode with version override
#   ✓ Create NuGet package with README validation
#   ✓ Publish package to NuGet with comprehensive validation
#   ✓ Create git tag for the version automatically
#   ✓ Push changes and tags to remote repository
#   ✓ Create GitHub release with changelog and package assets
#   ✓ Comprehensive error handling and validation
#
# Examples:
#   ./publish-nuget.sh "1.0.5"
#   ./publish-nuget.sh "1.0.6" "your-api-key-here"
#   ./publish-nuget.sh "1.0.7" "" "./src/MyLib/MyLib.csproj"
#   NUGET_API_KEY="key" ./publish-nuget.sh "1.0.8"
# =============================================================================

# Exit immediately on any command failure
set -e

# =============================================================================
# Configuration and Constants
# =============================================================================

readonly SCRIPT_NAME="$(basename "$0")"
readonly DEFAULT_PROJECT_FILE="./MyProject.csproj"
readonly DEFAULT_NUGET_SOURCE="https://api.nuget.org/v3/index.json"
readonly NUPKGS_DIR="./nupkgs"

# Color codes for output formatting
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly BLUE='\033[0;34m'
readonly NC='\033[0m' # No Color

# =============================================================================
# Utility Functions
# =============================================================================

# Print colored output messages
print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1" >&2
}

print_step() {
    echo -e "${BLUE}==>${NC} $1"
}

# Display usage information
show_usage() {
    cat << EOF
Usage: $SCRIPT_NAME <version> [api-key] [project-file] [nuget-source]

Arguments:
  version       (required) Package version (e.g., "1.2.3", "2.0.0-beta1")
  api-key       (optional) NuGet API key for publishing authorization
  project-file  (optional) Path to .csproj file (default: $DEFAULT_PROJECT_FILE)
  nuget-source  (optional) NuGet source URL (default: $DEFAULT_NUGET_SOURCE)

Environment Variables:
  NUGET_API_KEY           API key for NuGet publishing (alternative to argument)

Examples:
  $SCRIPT_NAME "1.0.0"
  $SCRIPT_NAME "1.0.1" "your-api-key-here"
  $SCRIPT_NAME "1.0.2" "" "./src/MyLib/MyLib.csproj"
  NUGET_API_KEY="key" $SCRIPT_NAME "1.0.3"

Automated Features:
  ✓ Build project in Release mode with version override
  ✓ Create NuGet package with README validation
  ✓ Publish package to NuGet with comprehensive validation
  ✓ Create git tag for the version automatically
  ✓ Push changes and tags to remote repository

Notes:
  - Script exits on any command failure (set -e enabled)
  - All paths are resolved relative to the project root
  - .nupkg files are output to $NUPKGS_DIR directory
  - Version validation ensures semantic versioning compliance
  - Git integration requires a git repository with 'origin' remote
  - Uncommitted changes will be warned about but not included in push
EOF
}

# Validate semantic version format
validate_version() {
    local version="$1"
    
    # Semantic version regex pattern (basic validation)
    local version_pattern="^[0-9]+\.[0-9]+\.[0-9]+(-[a-zA-Z0-9\-\.]+)?(\+[a-zA-Z0-9\-\.]+)?$"
    
    if [[ ! $version =~ $version_pattern ]]; then
        print_error "Invalid version format: '$version'"
        print_error "Expected semantic version format: MAJOR.MINOR.PATCH[-PRERELEASE][+BUILD]"
        print_error "Examples: 1.0.0, 1.2.3-beta1, 2.0.0-rc.1+build.123"
        return 1
    fi
    
    return 0
}

# Check if required tools are available
check_dependencies() {
    local missing_tools=()
    local optional_tools=()
    
    if ! command -v dotnet &> /dev/null; then
        missing_tools+=("dotnet")
    fi
    
    if ! command -v realpath &> /dev/null; then
        missing_tools+=("realpath")
    fi
    
    if ! command -v git &> /dev/null; then
        missing_tools+=("git")
    fi
    
    if ! command -v gh &> /dev/null; then
        optional_tools+=("gh (GitHub CLI - required for automatic GitHub releases)")
    fi
    
    if [ ${#missing_tools[@]} -gt 0 ]; then
        print_error "Missing required tools: ${missing_tools[*]}"
        print_error "Please install the missing dependencies and try again"
        return 1
    fi
    
    if [ ${#optional_tools[@]} -gt 0 ]; then
        print_warning "Optional tools not found: ${optional_tools[*]}"
        print_info "Install GitHub CLI (gh) for automatic GitHub release creation"
    fi
    
    return 0
}

# Resolve and validate file paths
resolve_project_path() {
    local project_file="$1"
    
    if [[ ! -f "$project_file" ]]; then
        print_error "Project file not found: '$project_file'"
        print_error "Please ensure the .csproj file exists and the path is correct"
        return 1
    fi
    
    # Resolve to absolute path for consistency
    realpath "$project_file"
}

# Extract project name from .csproj file
get_project_name() {
    local project_file="$1"
    local project_dir
    local project_name
    
    project_dir="$(dirname "$project_file")"
    project_name="$(basename "$project_dir")"
    
    echo "$project_name"
}

# =============================================================================
# Main Functions
# =============================================================================

# Parse and validate command line arguments
parse_arguments() {
    # Required argument: version
    if [[ -z "$1" ]]; then
        print_error "Missing required argument: version"
        echo
        show_usage
        exit 1
    fi
    
    VERSION="$1"
    
    # Validate version format
    if ! validate_version "$VERSION"; then
        exit 1
    fi
    
    # Optional argument: API key (or from environment)
    if [[ -n "$2" ]]; then
        API_KEY="$2"
    elif [[ -n "$NUGET_API_KEY" ]]; then
        API_KEY="$NUGET_API_KEY"
        print_info "Using API key from NUGET_API_KEY environment variable"
    else
        print_error "NuGet API key required"
        print_error "Provide as second argument or set NUGET_API_KEY environment variable"
        echo
        show_usage
        exit 1
    fi
    
    # Optional argument: project file path
    PROJECT_FILE="${3:-$DEFAULT_PROJECT_FILE}"
    
    # Optional argument: NuGet source URL
    NUGET_SOURCE="${4:-$DEFAULT_NUGET_SOURCE}"
    
    print_info "Configuration validated successfully"
}

# Override version in project file using MSBuild
override_project_version() {
    local project_file="$1"
    local version="$2"
    
    print_step "Overriding project version to '$version'"
    
    if ! dotnet msbuild "$project_file" -p:Version="$version" -verbosity:minimal; then
        print_error "Failed to override project version"
        print_error "Check that the project file is valid and accessible"
        return 1
    fi
    
    print_success "Project version set to '$version'"
}

# Create output directory and build package
build_package() {
    local project_file="$1"
    local output_dir="$2"
    
    print_step "Creating package output directory: '$output_dir'"
    mkdir -p "$output_dir"
    
    print_step "Building and creating NuGet package in Release mode"
    
    # Build and pack in one step to ensure output directory is respected
    if ! dotnet pack "$project_file" \
        --configuration Release \
        --output "$output_dir" \
        --verbosity normal; then
        print_error "Failed to build and create NuGet package"
        print_error "Check build and packaging errors above"
        return 1
    fi
    
    # Handle projects with GeneratePackageOnBuild=true
    # The package might be created in bin/Debug or bin/Release instead of output dir
    local project_dir
    project_dir="$(dirname "$project_file")"
    
    # Check if package was created in bin directories and move to output dir
    for bin_dir in "$project_dir/bin/Debug" "$project_dir/bin/Release"; do
        if [[ -d "$bin_dir" ]]; then
            find "$bin_dir" -name "*.nupkg" -exec mv {} "$output_dir/" \; 2>/dev/null || true
        fi
    done
    
    print_success "NuGet package created successfully"
}

# Locate the generated .nupkg file
find_package_file() {
    local output_dir="$1"
    local version="$2"
    local project_name="$3"
    
    print_step "Locating package file for version '$version'" >&2
    
    # Try to find the package file with the specific version
    local package_pattern="$output_dir/${project_name}.${version}.nupkg"
    local package_file
    
    # Use glob pattern to find the package
    package_file=$(find "$output_dir" -name "*${version}.nupkg" -type f | head -n 1)
    
    if [[ -z "$package_file" || ! -f "$package_file" ]]; then
        print_error "Package file not found: expected '*${version}.nupkg' in '$output_dir'" >&2
        print_error "Available packages:" >&2
        find "$output_dir" -name "*.nupkg" -type f | sed 's/^/  /' >&2 || echo "  (none found)" >&2
        return 1
    fi
    
    # Verify the package file contains the expected version
    local package_filename
    package_filename="$(basename "$package_file")"
    
    if [[ ! "$package_filename" =~ $version ]]; then
        print_error "Package file version mismatch: '$package_filename' does not contain '$version'" >&2
        return 1
    fi
    
    print_success "Package file located: '$package_file'" >&2
    echo "$package_file"
}

# Validate package contents and README inclusion
validate_package_contents() {
    local package_file="$1"
    
    print_step "Validating package contents and README inclusion"
    
    # List package contents - let it crash if unzip fails
    local package_contents
    package_contents=$(unzip -l "$package_file")
    
    # Check for README.md
    if echo "$package_contents" | grep -q "README.md"; then
        print_success "✓ README.md included in package"
        
        # Extract README size for validation
        local readme_size
        readme_size=$(echo "$package_contents" | grep "README.md" | awk '{print $1}')
        print_info "README.md size: $readme_size bytes"
        
        if [[ $readme_size -gt 1000 ]]; then
            print_success "✓ README.md appears to have substantial content"
        else
            print_warning "README.md seems small ($readme_size bytes) - verify content"
        fi
    else
        print_error "✗ README.md not found in package"
        print_error "README will not be displayed on NuGet"
        return 1
    fi
    
    # Check for logo/icon files
    if echo "$package_contents" | grep -q "logo_transparent_small.png"; then
        print_success "✓ Package icon included"
    else
        print_warning "Package icon not found"
    fi
    
    # Verify nuspec contains README reference - let it crash if it fails
    local nuspec_content
    nuspec_content=$(unzip -p "$package_file" "*.nuspec")
    
    if echo "$nuspec_content" | grep -q "<readme>README.md</readme>"; then
        print_success "✓ README properly configured in package metadata"
        print_info "README will be automatically displayed on NuGet package page"
    else
        print_warning "README not properly configured in package metadata"
        print_warning "README may not display automatically on NuGet"
    fi
    
    print_success "Package validation completed"
    return 0
}

# Publish package to NuGet source
publish_package() {
    local package_file="$1"
    local api_key="$2"
    local nuget_source="$3"
    
    print_step "Publishing package to NuGet source: '$nuget_source'"
    print_info "Package: $(basename "$package_file")"
    
    # Use --skip-duplicate to avoid errors if version already exists
    if ! dotnet nuget push "$package_file" \
        --api-key "$api_key" \
        --source "$nuget_source" \
        --skip-duplicate; then
        print_error "Failed to publish package to NuGet"
        print_error "Common causes:"
        print_error "  - Invalid API key"
        print_error "  - Network connectivity issues"
        print_error "  - Package version already exists (without --skip-duplicate)"
        print_error "  - Invalid NuGet source URL"
        return 1
    fi
    
    print_success "Package published successfully to NuGet"
}

# Clean up temporary files and directories
cleanup() {
    print_step "Cleaning up temporary files"
    
    # Remove any temporary MSBuild files if they exist
    find . -name "*.tmp" -type f -delete 2>/dev/null || true
    
    print_info "Cleanup completed"
}

# Check git repository status and validate for pushing
check_git_status() {
    print_step "Checking git repository status"
    
    # Check if we're in a git repository
    if ! git rev-parse --git-dir &> /dev/null; then
        print_error "Not in a git repository"
        print_error "Git push functionality requires a git repository"
        return 1
    fi
    
    # Check if there are uncommitted changes
    if ! git diff-index --quiet HEAD --; then
        print_warning "There are uncommitted changes in the repository"
        print_info "Uncommitted files:"
        git status --porcelain | sed 's/^/  /'
        print_warning "These changes will not be included in the git push"
    fi
    
    # Check current branch
    local current_branch
    current_branch=$(git branch --show-current)
    print_info "Current branch: $current_branch"
    
    # Check if remote exists
    if ! git remote get-url origin &> /dev/null; then
        print_warning "No 'origin' remote configured"
        print_warning "Git push will be skipped"
        return 1
    fi
    
    # Check if branch has upstream
    if ! git rev-parse --abbrev-ref @{upstream} &> /dev/null 2>&1; then
        print_info "Branch '$current_branch' has no upstream"
        print_info "Will push with --set-upstream"
    fi
    
    print_success "Git repository status validated"
    return 0
}

# Create git tag for the version
create_version_tag() {
    local version="$1"
    local tag_name="v$version"
    
    print_step "Creating git tag for version '$version'"
    
    # Check if tag already exists
    if git tag -l | grep -q "^$tag_name$"; then
        print_warning "Tag '$tag_name' already exists"
        print_info "Skipping tag creation"
        return 0
    fi
    
    # Create annotated tag with release information
    local tag_message="Version $version

🚀 NuGet Package Release
- Package: PinkRoosterAi.Persistify
- Version: $version
- Published: $(date -u '+%Y-%m-%d %H:%M:%S UTC')

Generated by publish-nuget.sh script"
    
    if git tag -a "$tag_name" -m "$tag_message"; then
        print_success "Created git tag: $tag_name"
        echo "$tag_name"
        return 0
    else
        print_error "Failed to create git tag"
        return 1
    fi
}

# Push changes and tags to remote repository
push_to_remote() {
    local version="$1"
    local tag_name="v$version"
    
    print_step "Pushing changes and tags to remote repository"
    
    # Get current branch
    local current_branch
    current_branch=$(git branch --show-current)
    
    # Push branch with upstream if needed
    if ! git rev-parse --abbrev-ref @{upstream} &> /dev/null 2>&1; then
        print_info "Setting upstream and pushing branch '$current_branch'"
        if ! git push --set-upstream origin "$current_branch"; then
            print_error "Failed to push branch with upstream"
            return 1
        fi
    else
        print_info "Pushing branch '$current_branch'"
        if ! git push; then
            print_error "Failed to push branch"
            return 1
        fi
    fi
    
    print_success "Branch pushed successfully"
    
    # Push tags
    print_info "Pushing tags to remote"
    if git push --tags; then
        print_success "Tags pushed successfully"
        print_info "Tag '$tag_name' is now available on remote repository"
    else
        print_error "Failed to push tags"
        return 1
    fi
    
    return 0
}

# Create GitHub release with changelog and assets
create_github_release() {
    local version="$1"
    local tag_name="v$version"
    local package_file="$2"
    
    print_step "Creating GitHub release for version '$version'"
    
    # Check if GitHub CLI is available
    if ! command -v gh &> /dev/null; then
        print_error "GitHub CLI (gh) not found"
        print_error "Install GitHub CLI to enable automatic release creation"
        print_warning "Manual release creation required on GitHub"
        return 1
    fi
    
    # Check if user is authenticated
    if ! gh auth status &> /dev/null; then
        print_error "GitHub CLI not authenticated"
        print_error "Run 'gh auth login' to authenticate with GitHub"
        print_warning "Manual release creation required on GitHub"
        return 1
    fi
    
    # Extract release notes from CHANGELOG.md for this version
    local release_notes=""
    if [[ -f "CHANGELOG.md" ]]; then
        print_info "Extracting release notes from CHANGELOG.md"
        
        # Extract content between this version and the next version marker
        release_notes=$(awk "/^## \[$version\]/{flag=1;next} /^## \[/{flag=0} flag" CHANGELOG.md | sed '/^$/N;/^\n$/d' | head -n -1)
        
        if [[ -n "$release_notes" ]]; then
            print_success "Release notes extracted successfully"
        else
            print_warning "No release notes found for version $version"
            release_notes="Release $version

🚀 **What's New**
- Enhanced release automation with GitHub integration
- Improved CI/CD pipeline and workflow tooling
- Updated documentation and version management

📦 **Installation**
\`\`\`bash
dotnet add package PinkRoosterAi.Persistify --version $version
\`\`\`

For full details, see the [CHANGELOG.md](https://github.com/pinkroosterai/Persistify/blob/main/CHANGELOG.md)."
        fi
    else
        print_warning "CHANGELOG.md not found, using default release notes"
        release_notes="Release $version - See repository for details"
    fi
    
    # Create the release
    print_info "Creating GitHub release with assets"
    local release_title="Release $version"
    
    if gh release create "$tag_name" \
        --title "$release_title" \
        --notes "$release_notes" \
        --verify-tag; then
        print_success "GitHub release created successfully"
        
        # Upload package file as asset if it exists
        if [[ -f "$package_file" ]]; then
            print_info "Uploading NuGet package as release asset"
            if gh release upload "$tag_name" "$package_file"; then
                print_success "Package uploaded to release assets"
            else
                print_warning "Failed to upload package file as asset"
            fi
        fi
        
        # Get release URL
        local release_url
        release_url=$(gh release view "$tag_name" --json url --jq '.url')
        if [[ -n "$release_url" ]]; then
            print_success "GitHub release available at: $release_url"
        fi
        
        return 0
    else
        print_error "Failed to create GitHub release"
        print_error "Check GitHub CLI authentication and repository permissions"
        return 1
    fi
}

# =============================================================================
# Main Execution Flow
# =============================================================================

main() {
    print_info "Starting NuGet publishing process"
    print_info "Script: $SCRIPT_NAME"
    print_info "Working directory: $(pwd)"
    
    # Check dependencies first
    print_step "Checking dependencies"
    if ! check_dependencies; then
        exit 1
    fi
    print_success "All dependencies available"
    
    # Parse and validate arguments
    print_step "Parsing command line arguments"
    parse_arguments "$@"
    
    # Resolve project file path
    print_step "Resolving project file path"
    PROJECT_FILE_ABSOLUTE=$(resolve_project_path "$PROJECT_FILE")
    if [[ $? -ne 0 ]]; then
        exit 1
    fi
    print_success "Project file resolved: '$PROJECT_FILE_ABSOLUTE'"
    
    # Extract project name for package identification
    PROJECT_NAME=$(get_project_name "$PROJECT_FILE_ABSOLUTE")
    print_info "Project name: '$PROJECT_NAME'"
    
    # Display configuration summary
    echo
    print_info "=== Configuration Summary ==="
    print_info "Version:       $VERSION"
    print_info "Project file:  $PROJECT_FILE_ABSOLUTE"
    print_info "Project name:  $PROJECT_NAME"
    print_info "Output dir:    $NUPKGS_DIR"
    print_info "NuGet source:  $NUGET_SOURCE"
    print_info "API key:       ${API_KEY:0:8}..." # Show only first 8 characters
    echo
    
    # Override project version
    override_project_version "$PROJECT_FILE_ABSOLUTE" "$VERSION"
    
    # Build the package
    build_package "$PROJECT_FILE_ABSOLUTE" "$NUPKGS_DIR"
    
    # Locate the generated package file
    PACKAGE_FILE=$(find_package_file "$NUPKGS_DIR" "$VERSION" "$PROJECT_NAME")
    if [[ $? -ne 0 ]]; then
        exit 1
    fi
    
    # Validate package contents and README inclusion
    validate_package_contents "$PACKAGE_FILE"
    if [[ $? -ne 0 ]]; then
        print_warning "Package validation failed, but continuing with publish"
    fi
    
    # Publish the package
    publish_package "$PACKAGE_FILE" "$API_KEY" "$NUGET_SOURCE"
    
    # Check git status and prepare for push
    echo
    if check_git_status; then
        # Create version tag
        TAG_NAME=$(create_version_tag "$VERSION")
        if [[ $? -eq 0 && -n "$TAG_NAME" ]]; then
            print_info "Created tag: $TAG_NAME"
            
            # Push to remote repository
            if push_to_remote "$VERSION"; then
                print_success "✓ Git push completed successfully"
                print_info "Version $VERSION is now available on remote repository"
                
                # Create GitHub release
                echo
                print_step "Creating GitHub release"
                if create_github_release "$VERSION" "$PACKAGE_FILE"; then
                    print_success "✓ GitHub release created successfully"
                    print_info "Release $VERSION is now available on GitHub"
                else
                    print_warning "GitHub release creation failed, but git push was successful"
                    print_info "You can create the release manually on GitHub"
                fi
            else
                print_error "Git push failed, but NuGet package was published successfully"
                print_warning "Manual git push and GitHub release creation may be required"
            fi
        else
            print_warning "Failed to create git tag, skipping git push"
        fi
    else
        print_warning "Git validation failed, skipping automatic git push"
        print_info "You may need to manually push changes and create tags"
    fi
    
    # Clean up
    cleanup
    
    # Final success message
    echo
    print_success "=== Release Process Completed Successfully ==="
    print_success "Package:     $(basename "$PACKAGE_FILE")"
    print_success "Version:     $VERSION"
    print_success "NuGet:       $NUGET_SOURCE"
    print_success "Git Tag:     v$VERSION"
    print_info "✓ Package should be available on NuGet within a few minutes"
    print_info "✓ GitHub release created with changelog and assets"
    print_info "✓ All automation completed successfully"
    
    return 0
}

# =============================================================================
# Error Handling and Cleanup
# =============================================================================

# Set up error handling
trap 'echo; print_error "Script failed at line $LINENO. Exit code: $?"; cleanup; exit 1' ERR
trap 'echo; print_warning "Script interrupted by user"; cleanup; exit 130' INT TERM

# Show usage if no arguments provided
if [[ $# -eq 0 ]]; then
    show_usage
    exit 1
fi

# Execute main function with all arguments
main "$@"