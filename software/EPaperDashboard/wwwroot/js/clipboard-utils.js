function copyApiKey(apiKey) {
    if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(apiKey).then(function() {
            showCopyToast();
        }).catch(function(err) {
            console.error('Clipboard API failed:', err);
            fallbackCopyApiKey(apiKey);
        });
    } else {
        fallbackCopyApiKey(apiKey);
    }
}

function fallbackCopyApiKey(apiKey) {
    const textArea = document.createElement('textarea');
    textArea.value = apiKey;
    textArea.style.position = 'fixed';
    textArea.style.left = '-999999px';
    document.body.appendChild(textArea);
    textArea.select();
    try {
        document.execCommand('copy');
        showCopyToast();
    } catch (err) {
        console.error('Fallback copy failed:', err);
        alert('Failed to copy API key. Please copy manually: ' + apiKey);
    }
    document.body.removeChild(textArea);
}

function showCopyToast() {
    var toast = document.getElementById('copy-toast');
    toast.style.display = 'block';
    setTimeout(hideCopyToast, 2000);
}

function hideCopyToast() {
    var toast = document.getElementById('copy-toast');
    toast.style.display = 'none';
}
