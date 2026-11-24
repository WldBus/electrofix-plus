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
            // выключенная кнопка не попадает в данные формы — значение нажатой кнопки (status=done) кладу в скрытое поле
            if (e.submitter && e.submitter.name) {
                const hidden = document.createElement('input');
                hidden.type = 'hidden';
                hidden.name = e.submitter.name;
                hidden.value = e.submitter.value;
                form.appendChild(hidden);
            }
            form.querySelectorAll('button[type=submit], button:not([type])').forEach(b => {
                b.disabled = true;
                b.insertAdjacentHTML('afterbegin', '<span class="spinner-border spinner-border-sm me-1"></span>');
            });
        });
    });

    // переход по обычным ссылкам внутри сайта — тоже загрузка
    document.querySelectorAll('a[href^="/"]:not([target])').forEach(a => {
        a.addEventListener('click', e => {
            // якорь на этой же странице («/#prices» на главной) — перезагрузки не будет
            const samePage = a.hash && a.pathname === location.pathname && a.search === location.search;
            if (!samePage && !e.ctrlKey && !e.metaKey && !e.shiftKey) {
                efLoading();
            }
        });
    });

    // фильтры в списках применяются сразу при выборе
    document.querySelectorAll('[data-autosubmit]').forEach(el => {
        el.addEventListener('change', () => el.form.requestSubmit());
    });
});

// переключатель темы в шапке: запоминаю выбор в браузере (если хранилище доступно)
document.querySelectorAll('[data-theme-toggle]').forEach(btn => {
    btn.addEventListener('click', () => {
        const root = document.documentElement;
        const next = root.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark';
        root.setAttribute('data-bs-theme', next);
        try {
            localStorage.setItem('ef-theme', next);
        } catch (e) {
            // в приватном режиме не сохранится — тема просто сбросится при следующем заходе
        }
    });
});

// вкладки прайса на главной: стрелками тоже можно переключать
const tabs = [...document.querySelectorAll('.ef-tab')];

function efShowTab(tab, focus) {
    tabs.forEach(t => {
        const on = t === tab;
        t.setAttribute('aria-selected', on ? 'true' : 'false');
        t.tabIndex = on ? 0 : -1;
        document.getElementById(t.dataset.tab).classList.toggle('is-active', on);
    });
    if (focus) {
        tab.focus();
    }
}

tabs.forEach((tab, i) => {
    tab.tabIndex = tab.getAttribute('aria-selected') === 'true' ? 0 : -1;
    tab.addEventListener('click', () => efShowTab(tab));
    tab.addEventListener('keydown', e => {
        const step = e.key === 'ArrowRight' ? 1 : e.key === 'ArrowLeft' ? -1 : 0;
        if (step) {
            e.preventDefault();
            efShowTab(tabs[(i + step + tabs.length) % tabs.length], true);
        }
    });
});

// «Выбрать» в прайсе: ставлю работу в форму заявки без перезагрузки страницы
const serviceSelect = document.getElementById('requestService');

function efMarkPicked(id) {
    document.querySelectorAll('[data-service-row]').forEach(row => {
        const on = row.dataset.serviceRow === id;
        row.classList.toggle('is-picked', on);
        row.querySelector('[data-pick]').textContent = on ? 'Выбрано' : 'Выбрать';
    });
}

if (serviceSelect) {
    document.querySelectorAll('[data-pick]').forEach(a => {
        a.addEventListener('click', e => {
            e.preventDefault();
            e.stopImmediatePropagation();
            serviceSelect.value = a.dataset.pick;
            efMarkPicked(a.dataset.pick);
            document.getElementById('request').scrollIntoView();
            serviceSelect.focus({ preventScroll: true });
        });
    });
    serviceSelect.addEventListener('change', () => efMarkPicked(serviceSelect.value));
}

// способ передачи: филиал нужен для «Принесу сам», адрес — для курьера и выезда мастера
const deliveryRadios = document.querySelectorAll('[data-delivery]');

function efShowDelivery() {
    const method = [...deliveryRadios].find(r => r.checked)?.value;
    document.querySelectorAll('[data-delivery-for]').forEach(block => {
        block.hidden = !block.dataset.deliveryFor.split(' ').includes(method);
    });
}

deliveryRadios.forEach(r => r.addEventListener('change', efShowDelivery));
if (deliveryRadios.length) {
    efShowDelivery();
}

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
