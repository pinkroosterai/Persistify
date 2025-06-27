# Refactoring Plan: Non-Generic Persistence Providers

## Goal
Refactor the persistence providers to decouple generic type parameters and align file/table names with the dictionary name, implementing the change iteratively through the Deming cycle.

## Progress

### ✅ PLAN Phase
- [x] Analyzed current generic persistence architecture (`IPersistenceProvider<TValue>`)
- [x] Identified issues with tight coupling between value types and providers
- [x] Designed new non-generic architecture using runtime type information

### ✅ DO Phase  
- [x] Implemented new `IPersistenceProvider` interface (non-generic)
- [x] Created `JsonFilePersistenceProvider` (non-generic) with runtime type handling
- [x] Created `DatabasePersistenceProvider` (non-generic) with runtime type handling
- [x] Built `PersistenceProviderAdapter<TValue>` for backward compatibility
- [x] Updated builders to support both generic and non-generic patterns
- [x] Updated main factory methods to support non-generic usage

### ✅ CHECK Phase
- [x] Validated that the new providers compile successfully
- [x] Confirmed architectural flexibility - single provider can handle multiple value types
- [x] Verified dictionary naming conventions work correctly (file/table names based on `dictionaryName`)

### ✅ ACT Phase (Completed)
- [x] Removed "v2" references as requested
- [x] Fixed compilation errors in PersistentDictionary.cs
- [x] Update existing unit tests to work with new architecture
- [x] Remove obsolete generic provider code
- [x] Clean up sample applications
- [ ] Commit changes

## Current Status
✅ **REFACTORING COMPLETE**: All components successfully refactored to non-generic architecture. Library builds, all tests pass. Ready for final commit.

## Key Architectural Changes

### Before (Generic)
```csharp
var provider = PersistenceProviderBuilder.JsonFile<int>(); // Separate per type
var dict = new PersistentDictionary<int>(provider, "dict-name");
```

### After (Non-Generic)
```csharp
var provider = PersistenceProviderBuilder.JsonFile(); // Single provider
var intDict = provider.CreateDictionary<int>("numbers");     // numbers.json
var stringDict = provider.CreateDictionary<string>("text");  // text.json
```

## Breaking Changes Made
- Removed class-level generics in favor of runtime type parameters
- Uses `dictionaryName` as primary storage identifier (file/table names)
- Type safety maintained through generic factory methods
- Serialization handled dynamically based on `Type` parameter

## Files Modified
- `IPersistenceProvider.cs` - New non-generic interface
- `JsonFilePersistenceProvider.cs` - Refactored to non-generic
- `DatabasePersistenceProvider.cs` - Refactored to non-generic
- `IPersistenceProviderBuilder.cs` - Added non-generic builder interface
- `JsonFilePersistenceProviderBuilder.cs` - Updated to support both patterns
- `DatabasePersistenceProviderBuilder.cs` - Updated to support both patterns
- `PersistenceProviderBuilder.cs` - Updated factory methods
- `PersistentDictionary.cs` - Fixed pattern matching for new providers

## Next Steps
1. Update unit tests for new architecture
2. Remove obsolete generic provider code
3. Update sample applications
4. Final validation and commit