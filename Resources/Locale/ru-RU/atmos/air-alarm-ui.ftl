# Интерфейс

## Окно

air-alarm-ui-title = Воздушная тревога

air-alarm-ui-access-denied = Недостаточно доступа!

air-alarm-ui-window-pressure-label = Давление
air-alarm-ui-window-temperature-label = Температура
air-alarm-ui-window-alarm-state-label = Состояние

air-alarm-ui-window-address-label = Адрес
air-alarm-ui-window-device-count-label = Всего устройств
air-alarm-ui-window-resync-devices-label = Пересинхронизировать

air-alarm-ui-window-mode-label = Режим
air-alarm-ui-window-mode-select-locked-label = [bold][color=red] Сбой переключателя режимов! [/color][/bold]
air-alarm-ui-window-auto-mode-label = Авторежим

-air-alarm-state-name = { $state ->
    [normal] Норма
    [warning] Предупреждение
    [danger] Опасность
    [emagged] Взломан
   *[invalid] Ошибка
}

air-alarm-ui-window-listing-title = {$address} : {-air-alarm-state-name(state:$state)}
air-alarm-ui-window-pressure = {$pressure} кПа
air-alarm-ui-window-pressure-indicator = Давление: [color={$color}]{$pressure} кПа[/color]
air-alarm-ui-window-temperature = {$tempC} °C ({$temperature} K)
air-alarm-ui-window-temperature-indicator = Температура: [color={$color}]{$tempC} °C ({$temperature} K)[/color]
air-alarm-ui-window-alarm-state = [color={$color}]{-air-alarm-state-name(state:$state)}[/color]
air-alarm-ui-window-alarm-state-indicator = Состояние: [color={$color}]{-air-alarm-state-name(state:$state)}[/color]

air-alarm-ui-window-tab-vents = Вентиляции
air-alarm-ui-window-tab-scrubbers = Скрубберы
air-alarm-ui-window-tab-sensors = Датчики

air-alarm-ui-gases = {$gas}: {$amount} моль ({$percentage}%)
air-alarm-ui-gases-indicator = {$gas}: [color={$color}]{$amount} моль ({$percentage}%)[/color]

air-alarm-ui-mode-filtering = Фильтрация
air-alarm-ui-mode-wide-filtering = Фильтрация (широкая)
air-alarm-ui-mode-fill = Заполнение
air-alarm-ui-mode-panic = Паника
air-alarm-ui-mode-none = Нет

air-alarm-ui-pump-direction-siphoning = Откачка
air-alarm-ui-pump-direction-scrubbing = Очистка
air-alarm-ui-pump-direction-releasing = Выпуск

air-alarm-ui-pressure-bound-nobound = Без границ
air-alarm-ui-pressure-bound-internalbound = Внутренняя граница
air-alarm-ui-pressure-bound-externalbound = Внешняя граница
air-alarm-ui-pressure-bound-both = Обе

air-alarm-ui-widget-gas-filters = Фильтры газов

## Виджеты

### Общее

air-alarm-ui-widget-enable = Включено
air-alarm-ui-widget-copy = Скопировать настройки на похожие устройства
air-alarm-ui-widget-copy-tooltip = Копирует настройки этого устройства на все устройства этой вкладки воздушной тревоги.
air-alarm-ui-widget-ignore = Игнорировать
air-alarm-ui-atmos-net-device-label = Адрес: {$address}

### Вентиляции

air-alarm-ui-vent-pump-label = Направление вентиляции
air-alarm-ui-vent-pressure-label = Граница давления
air-alarm-ui-vent-external-bound-label = Внешняя граница
air-alarm-ui-vent-internal-bound-label = Внутренняя граница

### Скрубберы

air-alarm-ui-scrubber-pump-direction-label = Направление
air-alarm-ui-scrubber-volume-rate-label = Скорость (л)
air-alarm-ui-scrubber-wide-net-label = Широкий охват
air-alarm-ui-scrubber-select-all-gases-label = Выбрать все
air-alarm-ui-scrubber-deselect-all-gases-label = Снять все

### Пороги

air-alarm-ui-sensor-gases = Газы
air-alarm-ui-sensor-thresholds = Пороги
air-alarm-ui-thresholds-pressure-title = Пороги (кПа)
air-alarm-ui-thresholds-temperature-title = Пороги (K)
air-alarm-ui-thresholds-gas-title = Пороги (%)
air-alarm-ui-thresholds-upper-bound = Опасно выше
air-alarm-ui-thresholds-lower-bound = Опасно ниже
air-alarm-ui-thresholds-upper-warning-bound = Предупреждение выше
air-alarm-ui-thresholds-lower-warning-bound = Предупреждение ниже
air-alarm-ui-thresholds-copy = Скопировать пороги на все устройства
air-alarm-ui-thresholds-copy-tooltip = Копирует пороги датчиков этого устройства на все устройства этой вкладки воздушной тревоги.
