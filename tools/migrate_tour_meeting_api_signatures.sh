#!/usr/bin/env bash
# Applies semantic signature updates verified against current build 24302054.
# All affected calls occur after confirming that voteAreaPlayer is the local owner.
set -euo pipefail
root="${1:?pass the TOU-R source directory}"

find "$root" -type f -name '*.cs' -print0 | xargs -0 perl -pi -e '
  s/\bmeetingHud\.ClearVote\(\);/meetingHud.ClearVote(playerVoteArea.PlayerId, true);/g;
  s/\bMeetingHud\.Instance\.ClearVote\(\);/MeetingHud.Instance.ClearVote(playerVoteArea.PlayerId, true);/g;
'
