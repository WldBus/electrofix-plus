// Немного JS для сайта: индикатор загрузки и подтверждение опасных кнопок.
// Всё остальное делает сервер (страницы собираются в MVC)

function efLoading() {
    document.getElementById('ef-loading').hidden = false;
}

document.addEventListener('DOMContentLoaded', () => {
    // подтверждение для кнопок «Отменить», «Удалить» и т. п.
    document.querySelectorAll('[data-confirm]').forEach(el => {
        el.addEventListener('click', e => {
            if (!confirm(el.dataset.confirm)) {
                e.preventDefault();
                e.stopImmediatePropagation();
            }
        });
    });

    // пока форма уходит на сервер — показываю загрузку и блокирую кнопку от двойного нажатия
    document.querySelectorAll('form').forEach(form => {
        form.addEventListener('submit', e => {
            // если не прошла проверка полей jquery.validate, ничего не отправится
            if (e.defaultPrevented || (window.jQuery && jQuery(form).data('validator') && !jQuery(form).valid())) {
                return;
            }
            efLoading();
            form.querySelectorAll('button[type=submit], button:not([type])').forEach(b => {
                b.disabled = true;
                b.insertAdjacentHTML('afterbegin', '<span class="spinner-border spinner-border-sm me-1"></span>');
            });
        });
    });

    // переход по обычным ссылкам внутри сайта — тоже загрузка
    document.querySelectorAll('a[href^="/"]:not([target])').forEach(a => {
        a.addEventListener('click', e => {
            if (!e.ctrlKey && !e.metaKey && !e.shiftKey) {
                efLoading();
            }
        });
    });

    // фильтры в списках применяются сразу при выборе
    document.querySelectorAll('[data-autosubmit]').forEach(el => {
        el.addEventListener('change', () => el.form.requestSubmit());
    });
});

// при возврате «Назад» браузер показывает страницу из кэша — убираю индикатор
window.addEventListener('pageshow', () => {
    const loading = document.getElementById('ef-loading');
    if (loading) {
        loading.hidden = true;
    }
});

// MVC сам помечает числовые поля обязательными и пишет текст по-английски — подменяю.
// Выполняется сразу (скрипт в конце страницы), до того как jquery.validate прочитает атрибуты
document.querySelectorAll('[data-val-required^="The "]').forEach(el => el.dataset.valRequired = 'Заполните поле');
