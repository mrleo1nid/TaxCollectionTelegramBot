using TaxCollectionTelegramBot.Data.Entities;
using Telegram.Bot.Types.ReplyMarkups;

namespace TaxCollectionTelegramBot.Handlers;

public static class KeyboardBuilder
{
    public static InlineKeyboardMarkup MainMenuUser()
    {
        return new InlineKeyboardMarkup(
            new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("📋 Мои конфиги", "user:configs") },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📖 Инструкция", "user:instruction"),
                },
            }
        );
    }

    public static InlineKeyboardMarkup MainMenuAdmin(
        bool hasActiveCollection,
        Collection? activeCollection = null
    )
    {
        var rows = new List<InlineKeyboardButton[]>
        {
            new[] { InlineKeyboardButton.WithCallbackData("👥 Пользователи", "admin:users") },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    "🗑️ Удалить пользователя",
                    "admin:delete_user"
                ),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("➕ Добавить конфиг", "admin:add_config"),
                InlineKeyboardButton.WithCallbackData(
                    "📋 Конфиги пользователей",
                    "admin:user_configs"
                ),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📢 Уведомление всем", "admin:broadcast"),
            },
        };

        if (hasActiveCollection && activeCollection != null)
        {
            rows.Add(
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "📊 Статус сбора",
                        "admin:collection_status"
                    ),
                }
            );
            switch (activeCollection.Status)
            {
                case CollectionStatus.Pending:
                    rows.Add(
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData(
                                "✅ Завершить сбор",
                                "admin:finalize_collection"
                            ),
                        }
                    );
                    break;
                case CollectionStatus.AwaitingConfirmation:
                    rows.Add(
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData(
                                "💳 Перейти к этапу оплаты",
                                "admin:move_to_payment"
                            ),
                        }
                    );
                    break;
                case CollectionStatus.AwaitingPayment:
                    rows.Add(
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData(
                                "✅ Завершить сбор",
                                "admin:finalize_collection"
                            ),
                        }
                    );
                    break;
            }
            rows.Add(
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "❌ Отменить сбор",
                        "admin:cancel_collection"
                    ),
                }
            );
        }
        else if (hasActiveCollection)
        {
            rows.Add(
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "📊 Статус сбора",
                        "admin:collection_status"
                    ),
                }
            );
            rows.Add(
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "✅ Завершить сбор",
                        "admin:finalize_collection"
                    ),
                }
            );
            rows.Add(
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "❌ Отменить сбор",
                        "admin:cancel_collection"
                    ),
                }
            );
        }
        else
        {
            rows.Add(
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "💰 Начать сбор",
                        "admin:start_collection"
                    ),
                }
            );
            rows.Add(
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "📋 Результаты последнего сбора",
                        "admin:last_collection_results"
                    ),
                }
            );
        }

        return new InlineKeyboardMarkup(rows);
    }

    public static InlineKeyboardMarkup DeleteUserConfirmationKeyboard(long userId)
    {
        return new InlineKeyboardMarkup(
            new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "✅ Да, удалить",
                        $"admin:delete_user_confirm:{userId}"
                    ),
                    InlineKeyboardButton.WithCallbackData("❌ Отмена", "admin:menu"),
                },
            }
        );
    }

    public static InlineKeyboardMarkup BackToMainMenu(bool isAdmin)
    {
        return new InlineKeyboardMarkup(
            new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "⬅️ Главное меню",
                        isAdmin ? "admin:menu" : "user:menu"
                    ),
                },
            }
        );
    }

    public static InlineKeyboardMarkup UserList(IEnumerable<User> users, string callbackPrefix)
    {
        var buttons = users
            .Select(u =>
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        $"{u.FirstName ?? u.Username ?? u.TelegramId.ToString()}",
                        $"{callbackPrefix}:{u.TelegramId}"
                    ),
                }
            )
            .ToList();

        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "admin:menu") });

        return new InlineKeyboardMarkup(buttons);
    }

    public static InlineKeyboardMarkup ConfigList(
        IEnumerable<UserConfig> configs,
        bool isAdmin,
        long? ownerUserId = null
    )
    {
        var buttons = configs
            .Select(c =>
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        $"📄 {c.Name}",
                        ownerUserId.HasValue
                            ? $"config:view:{c.Id}:{ownerUserId.Value}"
                            : $"config:view:{c.Id}"
                    ),
                }
            )
            .ToList();

        buttons.Add(
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    "⬅️ Назад",
                    isAdmin ? "admin:menu" : "user:menu"
                ),
            }
        );

        return new InlineKeyboardMarkup(buttons);
    }

    public static InlineKeyboardMarkup ConfigActions(
        int configId,
        bool isAdmin,
        long? ownerUserId = null
    )
    {
        var buttons = new List<InlineKeyboardButton[]>();

        if (isAdmin)
        {
            buttons.Add(
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "🗑️ Удалить",
                        ownerUserId.HasValue
                            ? $"config:delete:{configId}:{ownerUserId.Value}"
                            : $"config:delete:{configId}"
                    ),
                }
            );
            buttons.Add(
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "✏️ Название",
                        ownerUserId.HasValue
                            ? $"config:edit_name:{configId}:{ownerUserId.Value}"
                            : $"config:edit_name:{configId}"
                    ),
                    InlineKeyboardButton.WithCallbackData(
                        "✏️ Текст",
                        ownerUserId.HasValue
                            ? $"config:edit_text:{configId}:{ownerUserId.Value}"
                            : $"config:edit_text:{configId}"
                    ),
                }
            );
        }

        var backCallback = isAdmin
            ? (ownerUserId.HasValue ? $"admin:user_configs:{ownerUserId.Value}" : "admin:menu")
            : "user:configs";
        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", backCallback) });

        return new InlineKeyboardMarkup(buttons);
    }

    public static InlineKeyboardMarkup CollectionParticipation(int collectionId)
    {
        return new InlineKeyboardMarkup(
            new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "✅ Участвую",
                        $"collection:join:{collectionId}"
                    ),
                    InlineKeyboardButton.WithCallbackData(
                        "❌ Не участвую",
                        $"collection:decline:{collectionId}"
                    ),
                },
            }
        );
    }

    public static InlineKeyboardMarkup CollectionConfirmation(int collectionId)
    {
        return new InlineKeyboardMarkup(
            new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "✅ Подтверждаю",
                        $"collection:confirm:{collectionId}"
                    ),
                    InlineKeyboardButton.WithCallbackData(
                        "❌ Отказываюсь",
                        $"collection:reject:{collectionId}"
                    ),
                },
            }
        );
    }

    public static InlineKeyboardMarkup CollectionPaid(int collectionId)
    {
        return new InlineKeyboardMarkup(
            new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "✅ Я оплатил",
                        $"collection:paid:{collectionId}"
                    ),
                },
            }
        );
    }

    public static InlineKeyboardMarkup CancelAction()
    {
        return new InlineKeyboardMarkup(
            new[] { new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel") } }
        );
    }

    /// <summary>
    /// Persistent reply keyboard with single "Cancel" button (stays at bottom above input).
    /// Use <see cref="RemoveReplyKeyboard"/> when exiting the state.
    /// </summary>
    public static ReplyKeyboardMarkup CancelReplyKeyboard()
    {
        return new ReplyKeyboardMarkup(new KeyboardButton("❌ Отмена")) { ResizeKeyboard = true };
    }

    /// <summary>
    /// Removes the reply keyboard (e.g. after cancel or successful step completion).
    /// </summary>
    public static ReplyKeyboardRemove RemoveReplyKeyboard()
    {
        return new ReplyKeyboardRemove();
    }
}
