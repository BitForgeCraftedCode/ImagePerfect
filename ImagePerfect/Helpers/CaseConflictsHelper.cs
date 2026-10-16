using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImagePerfect.Helpers
{
    public static class CaseConflictsHelper
    {

        /*
        * The goal is to find folder paths that differ only by letter case,
        * including conflicts between the database and disk and conflicts that exist entirely on disk.
        * Concat() combines the two collections into one sequence without removing duplicates between the two.
        * so the list may look like this:
        * 
        * C:\Photos\BeachTrip   ← database
        * C:\Photos\Vacation    ← database
        * C:\Photos\BeachTrip   ← disk
        * C:\Photos\beachtrip   ← disk
        * C:\Photos\Summer      ← disk
        * 
        * GroupBy then groups (case insensitively) strings that are identical -- thus its now List of List and all my beachtrip folders will be in one group
        * Select Distinct takes each group and removes exact duplicate strings so the BeachTrip that appears twice will now just be once (BeachTrip, beachtrip)
        * Where filters out groups that contain only one distinct path Thus we have a list of lists where each inner list has a case conflict group
        */
        public static List<List<string>> FindCaseConflictGroups(
            IEnumerable<string> existingPaths,
            IEnumerable<string> candidatePaths)
        {
            return existingPaths.Concat(candidatePaths)
                .GroupBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Distinct(StringComparer.Ordinal).ToList())
                .Where(group => group.Count() > 1)
                .ToList();
        }
    }
}
