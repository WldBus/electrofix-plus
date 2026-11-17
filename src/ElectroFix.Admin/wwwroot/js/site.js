// Графики на Chart.js. Данные кладу в страницу из C#, тут только общее оформление
const efColors = ['#2563eb', '#16a34a', '#f59e0b', '#dc2626', '#7c3aed', '#0891b2', '#db2777', '#65a30d', '#ea580c', '#64748b'];

function efChart(id, type, labels, datasets, options) {
    const canvas = document.getElementById(id);
    if (!canvas || typeof Chart === 'undefined') {
        return;
    }
    datasets.forEach((d, i) => {
        const color = d.color || efColors[i % efColors.length];
        if (type === 'doughnut' || type === 'pie') {
            d.backgroundColor = labels.map((_, j) => efColors[j % efColors.length]);
        } else {
            d.backgroundColor = type === 'line' ? color + '22' : color;
            d.borderColor = color;
            d.tension = 0.3;
        }
    });
    new Chart(canvas, {
        type: type,
        data: { labels: labels, datasets: datasets },
        options: Object.assign({
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: datasets.length > 1 || type === 'doughnut' } },
        }, options || {}),
    });
}

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
