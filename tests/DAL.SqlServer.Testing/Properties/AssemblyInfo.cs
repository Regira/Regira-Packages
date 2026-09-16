using NUnit.Framework;

// Deliberately serial. The fixtures create, back up, restore and drop real databases on one LocalDB
// instance; running them concurrently thrashes a single IO-bound server rather than finishing sooner.
