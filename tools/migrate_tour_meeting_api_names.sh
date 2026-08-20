#!/usr/bin/env bash
# Applies only mechanical, confirmed symbol renames from the 2025 TOU-R API
# to the current build 24302054 generated bindings.  Semantic signature changes
# (ClearVote / RpcVotingComplete) are intentionally handled separately.
set -euo pipefail
root="${1:?pass the TOU-R source directory}"

find "$root" -type f -name '*.cs' -print0 | xargs -0 perl -pi -e '
  s/\bTargetPlayerId\b/PlayerId/g;
  s/\bVotedFor\b/VotedForId/g;
  s/\bSetTargetPlayerId\b/SetVote/g;
  s/\bvoteComplete\b/DidVote/g;
  s/MeetingHud\.VoteStates\./MeetingHud.MeetingStates./g;
  s/(?<!MeetingHud\.)\bMeetingStates\./MeetingHud.MeetingStates./g;
'
