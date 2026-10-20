# Displayed as initiator of vote when no user creates the vote
ui-vote-initiator-server = Сервер

## Default.Votes

ui-vote-restart-title = Перезапуск раунда
ui-vote-restart-succeeded = Голосование за перезапуск успешно.
ui-vote-restart-failed = Голосование за перезапуск провалено (нужно { TOSTRING($ratio, "P0") }).
ui-vote-restart-fail-not-enough-ghost-players = Голосование за перезапуск провалено: нужно не менее { $ghostPlayerRequirement }% игроков-призраков. Сейчас их недостаточно.
ui-vote-restart-yes = Да
ui-vote-restart-no = Нет
ui-vote-restart-abstain = Воздержаться

ui-vote-gamemode-title = Следующий режим
ui-vote-gamemode-tie = Ничья в голосовании за режим! Выбран... { $picked }
ui-vote-gamemode-win = В голосовании за режим победил { $winner }!

ui-vote-map-title = Следующая карта
ui-vote-map-tie = Ничья в голосовании за карту! Выбрана... { $picked }
ui-vote-map-win = В голосовании за карту победила { $winner }!
ui-vote-map-notlobby = Голосовать за карту можно только в лобби до начала раунда!
ui-vote-map-notlobby-time = Голосовать за карту можно только в лобби, пока осталось { $time }!
ui-vote-map-invalid = Карта { $winner } стала недоступна после голосования! Она не будет выбрана!

# Votekick votes
ui-vote-votekick-unknown-initiator = Игрок
ui-vote-votekick-unknown-target = Неизвестный игрок
ui-vote-votekick-title = { $initiator } начал голосование за кик игрока: { $targetEntity }. Причина: { $reason }
ui-vote-votekick-yes = Да
ui-vote-votekick-no = Нет
ui-vote-votekick-abstain = Воздержаться
ui-vote-votekick-success = Голосование за кик { $target } успешно. Причина: { $reason }
ui-vote-votekick-failure = Голосование за кик { $target } провалено. Причина: { $reason }
ui-vote-votekick-not-enough-eligible = Недостаточно голосующих онлайн для кика: { $voters }/{ $requirement }
ui-vote-votekick-server-cancelled = Голосование за кик { $target } отменено сервером.
