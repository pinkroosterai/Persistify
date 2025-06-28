# Changelog

All notable changes to PinkRoosterAi.Persistify will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2025-06-28

### 🎉 **First Stable Release**

This marks the first stable release of PinkRoosterAi.Persistify, transitioning from beta (0.9.0) to production-ready status with comprehensive documentation, automation tooling, and enhanced reliability.

### 🚀 **Added**

#### **Development & Automation**
- **NuGet Publishing Automation**: Complete Bash script (`publish-nuget.sh`) for automated package publishing
  - Semantic version validation with prerelease/build support
  - Environment variable support for API keys (`NUGET_API_KEY`)
  - Comprehensive error handling and color-coded progress output
  - MSBuild version override and Release mode packaging
  - Package validation and NuGet source publishing
- **Publishing Examples**: Usage examples and testing scripts for automation workflows
- **Project Cleanup Automation**: Automated cleanup of build artifacts (38.8M space savings)

#### **Documentation**
- **Comprehensive README Rewrite**: Complete technical documentation overhaul
  - Professional structure with badges and visual architecture diagrams
  - Installation instructions for multiple package managers
  - Extensive code examples covering all usage patterns
  - Performance characteristics and thread safety documentation
  - Complete API reference with methods, properties, and events
  - Real-world usage scenarios (config stores, session management, analytics)
  - Testing guidance with unit and integration test examples
  - Contributing guidelines and development setup instructions
- **Build Reports**: Detailed cleanup metrics and project analysis reports

#### **Code Quality**
- **Production Code Cleanup**: Removed debug output from production code paths
- **Code Documentation**: Enhanced inline documentation and examples
- **Error Handling**: Improved error messages and validation throughout

### 🔧 **Changed**

#### **Architecture**
- **Non-Generic Provider Architecture**: Major refactoring from generic to non-generic provider pattern
  - Runtime type handling with improved flexibility
  - Simplified provider implementation while maintaining type safety
  - Backward compatibility through adapter pattern
  - Enhanced extensibility for custom providers

#### **Documentation Standards**
- **README Modernization**: Upgraded from basic overview to comprehensive technical reference
  - 500% content expansion (80 lines → 540+ lines)
  - Professional formatting with emojis, tables, and structured sections
  - Technical depth suitable for production evaluation and implementation

#### **Project Configuration**
- **NuGet Package Metadata**: Enhanced package authoring following best practices
  - Improved package description and tags
  - Proper README and icon packaging
  - License expression and repository information

### 🐛 **Fixed**

#### **Core Functionality**
- **Database Operations**: Fixed table name queries and missing table errors
- **Build Issues**: Resolved build errors related to type arguments and method calls
- **Upsert Operations**: Fixed `AddAndSaveAsync` to perform proper upsert instead of add-only
- **Package Configuration**: Corrected README.md path in project file for proper packaging
- **Assembly Signing**: Removed problematic `PublicSign` configuration to fix build errors

#### **File Operations**
- **Line Endings**: Fixed Windows/Unix line ending compatibility in scripts
- **Path Resolution**: Improved relative path handling throughout the project

### 📊 **Performance**

#### **Project Optimization**
- **Repository Size**: 90% reduction in repository size through intelligent cleanup
  - Removed 38.8M of build artifacts and temporary files
  - Git repository optimization and object database compression
  - Faster clone and development workflow operations

#### **Code Efficiency**
- **Memory Management**: Optimized async disposal patterns
- **Thread Safety**: Enhanced lock granularity and concurrent access patterns
- **I/O Operations**: Improved buffered stream handling for large data sets

### 🛡️ **Security**

#### **Input Validation**
- **SQL Injection Protection**: Enhanced parameterized queries and table name validation
- **Path Validation**: Improved file path resolution and security checks
- **Error Information**: Sanitized error messages to prevent information disclosure

### 🧪 **Testing**

#### **Test Infrastructure**
- **Validation Scripts**: Comprehensive testing automation for publishing workflows
- **Error Scenarios**: Enhanced test coverage for failure conditions
- **Integration Testing**: Improved cross-platform compatibility testing

### 📋 **Infrastructure**

#### **Build System**
- **Automation Scripts**: Production-ready build and publish automation
- **Dependency Management**: Streamlined package references and version management
- **Development Workflow**: Enhanced development setup and contribution guidelines

---

## [0.9.0] - 2025-06-XX (Previous Beta Release)

### **Beta Release Features**
- Thread-safe persistent dictionary implementation
- JSON file and database storage providers
- TTL-based caching with automatic eviction
- Batch operations with configurable triggers
- Polly-based retry logic with exponential backoff
- Builder pattern for fluent configuration
- Async/await support throughout
- Event-driven error reporting
- ServiceStack.OrmLite integration for database operations
- .NET 9.0 target framework

---

## Release Notes

### 🎯 **Migration Guide from 0.9.0 to 1.0.0**

No breaking changes - this release focuses on stability, documentation, and tooling improvements. Existing code will continue to work without modifications.

### 🔮 **Looking Forward**

Future releases will focus on:
- Additional storage provider implementations (Redis, Azure Blob, etc.)
- Performance optimizations and benchmarking
- Advanced caching strategies and eviction policies
- Enhanced monitoring and telemetry capabilities
- .NET AOT (Ahead-of-Time) compilation support

### 🤝 **Contributing**

We welcome contributions! Please see our [Contributing Guidelines](CONTRIBUTING.md) for details on:
- Development setup and build requirements
- Code style and testing standards
- Pull request submission process
- Issue reporting and feature requests

### 📞 **Support**

- **Issues**: [GitHub Issues](https://github.com/pinkroosterai/Persistify/issues)
- **Documentation**: [GitHub Wiki](https://github.com/pinkroosterai/Persistify/wiki)
- **NuGet Package**: [PinkRoosterAi.Persistify](https://www.nuget.org/packages/PinkRoosterAi.Persistify)

---

*For detailed technical information, see the [README.md](README.md) file.*