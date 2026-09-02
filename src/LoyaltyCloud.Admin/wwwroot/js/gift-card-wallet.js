window.loyaltyGiftCardWallet = {
    getUserAgent() { return navigator.userAgent || ""; },
    selectZero(element) { if (element && Number(element.value) === 0) element.select(); },
    downloadPass(base64, fileName) {
        const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
        const url = URL.createObjectURL(new Blob([bytes], { type: "application/vnd.apple.pkpass" }));
        const anchor = document.createElement("a");
        anchor.href = url; anchor.download = fileName; document.body.appendChild(anchor); anchor.click(); anchor.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    }
};
