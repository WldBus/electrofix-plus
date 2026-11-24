// Графики на Chart.js. Данные кладу в страницу из C#, тут только общее оформление
// Палитра графиков: приглушённые цвета, которые читаются и на светлом, и на тёмном фоне.
// Во вьюхах цвет задаю именем (color: 'green'), чтобы не размазывать hex по страницам
const efPalette = {
    blue: '#3B7DB0', green: '#3E9466', amber: '#D9A21B', red: '#C8524A', violet: '#7C6CC0',
    teal: '#2E9A9A', slate: '#7F8A90', orange: '#CC7A33', pink: '#B85C8A', olive: '#8A9A3B',
};
const efColors = Object.values(efPalette);

function efVar(name) {
    return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
}

// подписи и сетка графиков берут цвета из CSS-переменных текущей темы
function efChartTheme() {
    if (typeof Chart === 'undefined') {
        return;
    }
    Chart.defaults.color = efVar('--ef-muted');
    Chart.defaults.borderColor = efVar('--ef-line');
    Chart.defaults.font.family = '"IBM Plex Sans", system-ui, sans-serif';
    Object.values(Chart.instances).forEach(c => {
        // у кольцевых диаграмм граница сегментов — под цвет карточки
        if (c.config.type === 'doughnut') {
            c.data.datasets.forEach(d => d.borderColor = efVar('--ef-surface'));
        }
        c.update('none');
    });
}

function efChart(id, type, labels, datasets, options) {
    const canvas = document.getElementById(id);
    if (!canvas || typeof Chart === 'undefined') {
        return;
    }
    efChartTheme();
    datasets.forEach((d, i) => {
        const color = efPalette[d.color] || d.color || efColors[i % efColors.length];
        if (type === 'doughnut' || type === 'pie') {
            d.backgroundColor = labels.map((_, j) => efColors[j % efColors.length]);
            d.borderColor = efVar('--ef-surface');
        } else {
            d.backgroundColor = type === 'line' ? color + '22' : color;
            d.borderColor = color;
            d.tension = 0.3;
            if (type === 'line') {
                d.pointRadius = 2;
            } else {
                d.borderRadius = 2;
            }
        }
    });
    new Chart(canvas, {
        type: type,
        data: { labels: labels, datasets: datasets },
        options: Object.assign({
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: datasets.length > 1 || type === 'doughnut', labels: { boxWidth: 12, boxHeight: 12 } } },
        }, options || {}),
    });
}

// переключатель темы внизу меню; графики перекрашиваю сразу, без перезагрузки
document.querySelectorAll('[data-theme-toggle]').forEach(btn => {
    btn.addEventListener('click', () => {
        const root = document.documentElement;
        const next = root.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark';
        root.setAttribute('data-bs-theme', next);
        try {
            localStorage.setItem('ef-theme', next);
        } catch (e) {
            // в приватном режиме не сохранится — не страшно
        }
        efChartTheme();
    });
});

// случайный пароль для нового сотрудника: 14 символов, есть большие и маленькие буквы, цифры и знаки
function efGeneratePassword(inputId) {
    const sets = ['ABCDEFGHJKLMNPQRSTUVWXYZ', 'abcdefghijkmnopqrstuvwxyz', '23456789', '!@#$%&*?'];
    const all = sets.join('');
    const random = n => {
        const a = new Uint32Array(1);
        crypto.getRandomValues(a);
        return a[0] % n;
    };
    const chars = sets.map(s => s[random(s.length)]);
    while (chars.length < 14) {
        chars.push(all[random(all.length)]);
    }
    // перемешиваю, чтобы первые четыре символа не шли всегда в одном порядке
    for (let i = chars.length - 1; i > 0; i--) {
        const j = random(i + 1);
        [chars[i], chars[j]] = [chars[j], chars[i]];
    }
    const input = document.getElementById(inputId);
    input.type = 'text';
    input.value = chars.join('');
}

function efCopy(inputId, button) {
    const value = document.getElementById(inputId).value;
    if (!value) {
        return;
    }
    navigator.clipboard.writeText(value).then(() => {
        const old = button.innerHTML;
        button.innerHTML = '<i class="bi bi-check2"></i>';
        setTimeout(() => button.innerHTML = old, 1500);
    });
}

function efBulkCount() {
    const count = document.querySelectorAll('input[name="ids"]:checked').length;
    document.querySelectorAll('[data-bulk-count]').forEach(el => el.textContent = count);
    document.querySelectorAll('[data-bulk-button]').forEach(el => el.disabled = count === 0);
}

document.addEventListener('DOMContentLoaded', () => {
    // «выбрать все» в списках с массовым удалением
    document.querySelectorAll('[data-check-all]').forEach(all => {
        all.addEventListener('change', () => {
            document.querySelectorAll('input[name="ids"]').forEach(c => c.checked = all.checked);
            efBulkCount();
        });
    });
    document.querySelectorAll('input[name="ids"]').forEach(c => c.addEventListener('change', efBulkCount));
    efBulkCount();

    // подтверждение для опасных кнопок
    document.querySelectorAll('[data-confirm]').forEach(el => {
        el.addEventListener('click', e => {
            if (!confirm(el.dataset.confirm)) {
                e.preventDefault();
            }
        });
    });

    // окно блокировки: подставляю адрес формы и email пользователя
    const modal = document.getElementById('blockModal');
    if (modal) {
        modal.addEventListener('show.bs.modal', e => {
            const button = e.relatedTarget;
            modal.querySelector('form').action = button.dataset.action;
            modal.querySelector('[data-who]').textContent = button.dataset.who;
            modal.querySelector('textarea').value = '';
        });
    }
});

// MVC сам помечает числовые поля обязательными и пишет текст по-английски — подменяю.
// Выполняется сразу (скрипт в конце страницы), до того как jquery.validate прочитает атрибуты
document.querySelectorAll('[data-val-required^="The "]').forEach(el => el.dataset.valRequired = 'Заполните поле');
