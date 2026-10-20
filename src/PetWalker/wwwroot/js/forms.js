// Shared form helpers.

// Phone fields: digits only, at most 11.
for (const input of document.querySelectorAll('input[data-phone-tr]')) {
    input.addEventListener('input', () => { input.value = input.value.replace(/\D/g, '').slice(0, 11); });
}

// Turkish messages for the browser's own checks, so forms can't be sent with empty or invalid fields.
const messages = {
    valueMissing: 'Bu alan zorunludur.',
    typeMismatch: 'Geçerli bir e-posta adresi girin.',
};
for (const input of document.querySelectorAll('form[data-tr-validation] input, form[data-tr-validation] textarea')) {
    input.addEventListener('invalid', () => {
        let text = '';
        if (input.type === 'checkbox') text = 'KVKK koşullarını kabul etmelisiniz.';
        else if (input.validity.patternMismatch) text = input.title || 'Geçersiz biçim.';
        else if (input.validity.tooShort)
            text = input.type === 'password'
                ? `Şifre en az ${input.minLength} karakter olmalı.`
                : `En az ${input.minLength} karakter yazmalısın.`;
        else text = Object.entries(messages).find(([k]) => input.validity[k])?.[1] ?? '';
        input.setCustomValidity(text);
    });
    input.addEventListener('input', () => input.setCustomValidity(''));
    input.addEventListener('change', () => input.setCustomValidity(''));
}
